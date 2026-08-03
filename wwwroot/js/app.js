/**
 * PULSE System Monitor - SignalR Real-Time Client
 */

document.addEventListener('DOMContentLoaded', () => {
    // Canvas Chart Helpers
    class TimeSeriesChart {
        constructor(canvasId, options = {}) {
            this.canvas = document.getElementById(canvasId);
            if (!this.canvas) return;
            this.ctx = this.canvas.getContext('2d');
            this.maxHistory = options.maxHistory || 40;
            this.seriesList = options.series || [{ color: '#00f2fe' }];
            this.minVal = options.minVal ?? 0;
            this.maxVal = options.maxVal ?? 100;
            this.autoScale = options.autoScale || false;

            this.dataHistory = this.seriesList.map(() => new Array(this.maxHistory).fill(0));
            this.resizeCanvas();
            window.addEventListener('resize', () => this.resizeCanvas());
        }

        resizeCanvas() {
            if (!this.canvas) return;
            const rect = this.canvas.getBoundingClientRect();
            this.canvas.width = rect.width * window.devicePixelRatio;
            this.canvas.height = rect.height * window.devicePixelRatio;
            this.ctx.scale(window.devicePixelRatio, window.devicePixelRatio);
            this.w = rect.width;
            this.h = rect.height;
            this.render();
        }

        pushValues(values) {
            values.forEach((val, idx) => {
                if (this.dataHistory[idx]) {
                    this.dataHistory[idx].push(val);
                    if (this.dataHistory[idx].length > this.maxHistory) {
                        this.dataHistory[idx].shift();
                    }
                }
            });
            this.render();
        }

        render() {
            if (!this.ctx || !this.w || !this.h) return;
            const ctx = this.ctx;
            ctx.clearRect(0, 0, this.w, this.h);

            // Draw grid lines
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
            ctx.lineWidth = 1;
            for (let y = 0; y <= this.h; y += this.h / 4) {
                ctx.beginPath();
                ctx.moveTo(0, y);
                ctx.lineTo(this.w, y);
                ctx.stroke();
            }

            // Determine scaling
            let currentMax = this.maxVal;
            let currentMin = this.minVal;

            if (this.autoScale) {
                let maxFound = 10;
                this.dataHistory.forEach(series => {
                    series.forEach(v => { if (v > maxFound) maxFound = v; });
                });
                currentMax = maxFound * 1.2;
            }

            const range = currentMax - currentMin || 1;

            // Draw series
            this.dataHistory.forEach((series, sIdx) => {
                const sConf = this.seriesList[sIdx] || { color: '#00f2fe' };
                if (series.length < 2) return;

                const stepX = this.w / (this.maxHistory - 1);
                ctx.beginPath();

                series.forEach((val, i) => {
                    const x = i * stepX;
                    const normY = (val - currentMin) / range;
                    const y = this.h - (normY * (this.h - 10) + 5);

                    if (i === 0) ctx.moveTo(x, y);
                    else ctx.lineTo(x, y);
                });

                // Stroke line
                ctx.strokeStyle = sConf.color;
                ctx.lineWidth = 2.5;
                ctx.stroke();

                // Fill gradient under curve
                ctx.lineTo((series.length - 1) * stepX, this.h);
                ctx.lineTo(0, this.h);
                ctx.closePath();

                const grad = ctx.createLinearGradient(0, 0, 0, this.h);
                grad.addColorStop(0, sConf.fillColor || (sConf.color + '33'));
                grad.addColorStop(1, 'transparent');
                ctx.fillStyle = grad;
                ctx.fill();
            });
        }
    }

    // Initialize Charts
    const cpuChart = new TimeSeriesChart('cpu-chart', {
        maxHistory: 50,
        minVal: 0,
        maxVal: 100,
        series: [{ color: '#00f2fe', fillColor: 'rgba(0, 242, 254, 0.25)' }]
    });

    const ramChart = new TimeSeriesChart('ram-chart', {
        maxHistory: 50,
        minVal: 0,
        maxVal: 100,
        series: [{ color: '#8a2be2', fillColor: 'rgba(138, 43, 226, 0.25)' }]
    });

    const netChart = new TimeSeriesChart('net-chart', {
        maxHistory: 50,
        autoScale: true,
        series: [
            { color: '#00f2fe', fillColor: 'rgba(0, 242, 254, 0.15)' }, // Download
            { color: '#8a2be2', fillColor: 'rgba(138, 43, 226, 0.15)' }  // Upload
        ]
    });

    // App State
    let latestProcesses = [];
    let activeKillPid = null;

    // DOM References
    const statusEl = document.getElementById('connection-status');
    const procTableBody = document.getElementById('proc-table-body');
    const procSearchInput = document.getElementById('proc-search');
    const procSortSelect = document.getElementById('proc-sort');
    const procCountBadge = document.getElementById('proc-filtered-count');

    // Modal DOM
    const killModal = document.getElementById('kill-modal');
    const modalProcName = document.getElementById('modal-proc-name');
    const modalProcPid = document.getElementById('modal-proc-pid');
    const modalCancelBtn = document.getElementById('modal-cancel');
    const modalConfirmBtn = document.getElementById('modal-confirm');

    // Format Helpers
    function formatUptime(seconds) {
        const d = Math.floor(seconds / (3600 * 24));
        const h = Math.floor((seconds % (3600 * 24)) / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = Math.floor(seconds % 60);
        return `${d > 0 ? d + 'd ' : ''}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    }

    function formatSpeed(kbps) {
        if (kbps >= 1024) {
            return (kbps / 1024).toFixed(1) + ' MB/s';
        }
        return kbps.toFixed(0) + ' KB/s';
    }

    // Build SignalR Connection
    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/metrics')
        .withAutomaticReconnect([0, 1000, 2000, 5000])
        .build();

    connection.onreconnecting(() => {
        statusEl.className = 'status-indicator warning';
        statusEl.querySelector('.status-text').textContent = 'Reconnecting...';
    });

    connection.onreconnected(() => {
        statusEl.className = 'status-indicator';
        statusEl.querySelector('.status-text').textContent = 'Live Streaming';
    });

    connection.onclose(() => {
        statusEl.className = 'status-indicator danger';
        statusEl.querySelector('.status-text').textContent = 'Disconnected';
    });

    // Handle Incoming Live System Telemetry
    connection.on('ReceiveMetrics', (snapshot) => {
        // Update connection status label
        statusEl.className = 'status-indicator';
        statusEl.querySelector('.status-text').textContent = 'Live Streaming';

        // 1. System Info
        if (snapshot.systemInfo) {
            document.getElementById('sys-hostname').textContent = snapshot.systemInfo.hostName || '---';
            document.getElementById('sys-os').textContent = snapshot.systemInfo.osDescription || '---';
            document.getElementById('sys-cpuname').textContent = snapshot.systemInfo.cpuName || '---';
            document.getElementById('sys-uptime').textContent = formatUptime(snapshot.systemInfo.uptimeSeconds || 0);
        }

        // 2. CPU Metrics
        if (snapshot.cpu) {
            const totalUsage = snapshot.cpu.overallUsage || 0;
            document.getElementById('cpu-total-text').textContent = totalUsage.toFixed(1) + '%';
            document.getElementById('cpu-process-count').textContent = snapshot.cpu.processCount || 0;
            document.getElementById('cpu-thread-count').textContent = snapshot.cpu.threadCount || 0;
            document.getElementById('cpu-core-count').textContent = snapshot.cpu.coreUsages?.length || 0;

            cpuChart.pushValues([totalUsage]);
            updateCoreMatrix(snapshot.cpu.coreUsages || []);
        }

        // 3. Memory Metrics
        if (snapshot.memory) {
            const mem = snapshot.memory;
            document.getElementById('ram-pct-text').textContent = mem.usagePercentage.toFixed(1) + '%';
            document.getElementById('ram-used').textContent = (mem.usedMb / 1024).toFixed(1) + ' GB';
            document.getElementById('ram-free').textContent = (mem.freeMb / 1024).toFixed(1) + ' GB';
            document.getElementById('ram-total').textContent = (mem.totalMb / 1024).toFixed(1) + ' GB';
            document.getElementById('ram-progress-fill').style.width = Math.min(100, mem.usagePercentage) + '%';

            ramChart.pushValues([mem.usagePercentage]);
        }

        // 4. Storage Drives
        if (snapshot.disks) {
            updateDrives(snapshot.disks);
        }

        // 5. Network Metrics
        if (snapshot.networkInterfaces) {
            let totalDown = 0;
            let totalUp = 0;
            snapshot.networkInterfaces.forEach(ni => {
                totalDown += ni.downloadSpeedKbps || 0;
                totalUp += ni.uploadSpeedKbps || 0;
            });

            document.getElementById('net-down-speed').textContent = formatSpeed(totalDown);
            document.getElementById('net-up-speed').textContent = formatSpeed(totalUp);

            netChart.pushValues([totalDown, totalUp]);
            updateNetworkInterfaces(snapshot.networkInterfaces);
        }

        // 6. Processes
        if (snapshot.processes) {
            latestProcesses = snapshot.processes;
            renderProcessTable();
        }
    });

    // Core Matrix Renderer
    function updateCoreMatrix(coreUsages) {
        const matrixContainer = document.getElementById('core-matrix');
        if (!matrixContainer) return;

        // Reuse existing core items if possible
        if (matrixContainer.children.length !== coreUsages.length) {
            matrixContainer.innerHTML = '';
            coreUsages.forEach((_, idx) => {
                const item = document.createElement('div');
                item.className = 'core-item';
                item.id = `core-item-${idx}`;
                item.innerHTML = `
                    <div class="core-item-head">
                        <span>C${idx}</span>
                        <span class="core-val" id="core-val-${idx}">0%</span>
                    </div>
                    <div class="core-bar-bg">
                        <div class="core-bar-fill" id="core-fill-${idx}"></div>
                    </div>
                `;
                matrixContainer.appendChild(item);
            });
        }

        coreUsages.forEach((usage, idx) => {
            const valEl = document.getElementById(`core-val-${idx}`);
            const fillEl = document.getElementById(`core-fill-${idx}`);
            if (valEl && fillEl) {
                valEl.textContent = usage.toFixed(0) + '%';
                fillEl.style.width = Math.min(100, usage) + '%';
                fillEl.style.backgroundColor = usage > 85 ? 'var(--accent-danger)' : usage > 65 ? 'var(--accent-amber)' : 'var(--accent-cyan)';
            }
        });
    }

    // Drives Renderer
    function updateDrives(disks) {
        const listEl = document.getElementById('drives-list');
        if (!listEl) return;

        listEl.innerHTML = disks.map(drive => `
            <div class="drive-item">
                <div class="drive-head">
                    <div class="drive-name-group">
                        <div class="drive-icon">
                            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" width="18" height="18">
                                <rect x="2" y="4" width="20" height="16" rx="2"></rect>
                                <path d="M6 12h4"></path>
                            </svg>
                        </div>
                        <div class="drive-info">
                            <div class="title">${drive.volumeLabel} (${drive.name})</div>
                            <div class="subtitle">${drive.driveFormat} &bull; ${drive.driveType}</div>
                        </div>
                    </div>
                    <div class="drive-usage-text">${drive.usedGb} GB / ${drive.totalGb} GB</div>
                </div>
                <div class="drive-bar-bg">
                    <div class="drive-bar-fill" style="width: ${Math.min(100, drive.usagePercentage)}%"></div>
                </div>
            </div>
        `).join('');
    }

    // Network Interfaces Renderer
    function updateNetworkInterfaces(ifaces) {
        const listEl = document.getElementById('net-interfaces-list');
        if (!listEl) return;

        listEl.innerHTML = ifaces.map(ni => `
            <div class="net-iface-row">
                <span><strong>${ni.name}</strong> (${ni.ipAddress || 'No IPv4'})</span>
                <span>↓ ${formatSpeed(ni.downloadSpeedKbps)} | ↑ ${formatSpeed(ni.uploadSpeedKbps)}</span>
            </div>
        `).join('');
    }

    // Process Table Filter, Sort & Render
    function renderProcessTable() {
        if (!procTableBody) return;

        const query = (procSearchInput.value || '').toLowerCase().trim();
        const sortBy = procSortSelect.value || 'ram';

        let filtered = latestProcesses.filter(p => 
            p.name.toLowerCase().includes(query) || p.pid.toString().includes(query)
        );

        // Sorting
        filtered.sort((a, b) => {
            if (sortBy === 'ram') return b.workingSetMb - a.workingSetMb;
            if (sortBy === 'cpu') return b.cpuPercentage - a.cpuPercentage;
            if (sortBy === 'name') return a.name.localeCompare(b.name);
            if (sortBy === 'pid') return a.pid - b.pid;
            return 0;
        });

        procCountBadge.textContent = `${filtered.length} processes`;

        // Calculate max RAM for progress bar
        const maxRam = Math.max(...filtered.map(p => p.workingSetMb), 1);

        procTableBody.innerHTML = filtered.map(p => `
            <tr>
                <td class="proc-pid">${p.pid}</td>
                <td class="proc-name">${escapeHtml(p.name)}</td>
                <td>
                    <div class="proc-mem-bar">
                        <span class="proc-mem-val">${p.workingSetMb} MB</span>
                        <div class="proc-mem-mini-bg">
                            <div class="proc-mem-mini-fill" style="width: ${Math.min(100, (p.workingSetMb / maxRam) * 100)}%"></div>
                        </div>
                    </div>
                </td>
                <td>${p.cpuPercentage.toFixed(1)}%</td>
                <td>${p.threadCount}</td>
                <td>
                    <button class="btn-kill" data-pid="${p.pid}" data-name="${escapeHtml(p.name)}">End Task</button>
                </td>
            </tr>
        `).join('');

        // Attach event listeners to kill buttons
        procTableBody.querySelectorAll('.btn-kill').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const pid = parseInt(e.target.getAttribute('data-pid'));
                const name = e.target.getAttribute('data-name');
                openKillModal(pid, name);
            });
        });
    }

    function escapeHtml(str) {
        return str.replace(/[&<>'"]/g, 
            tag => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[tag] || tag)
        );
    }

    // Filter & Sort Event Listeners
    procSearchInput.addEventListener('input', renderProcessTable);
    procSortSelect.addEventListener('change', renderProcessTable);

    // Modal Kill Dialog Handlers
    function openKillModal(pid, name) {
        activeKillPid = pid;
        modalProcPid.textContent = pid;
        modalProcName.textContent = name;
        killModal.classList.remove('hidden');
    }

    function closeKillModal() {
        activeKillPid = null;
        killModal.classList.add('hidden');
    }

    modalCancelBtn.addEventListener('click', closeKillModal);

    modalConfirmBtn.addEventListener('click', async () => {
        if (!activeKillPid) return;
        const pidToKill = activeKillPid;
        closeKillModal();

        try {
            // Invoke kill via SignalR Hub or REST API
            const result = await connection.invoke("KillProcess", pidToKill);
            if (result && result.success) {
                // Remove immediately from UI
                latestProcesses = latestProcesses.filter(p => p.pid !== pidToKill);
                renderProcessTable();
            } else {
                alert(result?.message || 'Failed to kill process');
            }
        } catch (err) {
            console.error('Error terminating process:', err);
            // Fallback REST call
            fetch(`/api/process/${pidToKill}`, { method: 'DELETE' })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        latestProcesses = latestProcesses.filter(p => p.pid !== pidToKill);
                        renderProcessTable();
                    } else {
                        alert(data.message || 'Failed to kill process');
                    }
                })
                .catch(e => alert('Network error trying to kill process: ' + e.message));
        }
    });

    // Start SignalR Connection
    async function startSignalR() {
        try {
            await connection.start();
            console.log('SignalR Telemetry connected successfully.');
            statusEl.className = 'status-indicator';
            statusEl.querySelector('.status-text').textContent = 'Live Streaming';
        } catch (err) {
            console.error('SignalR Connection Error: ', err);
            statusEl.className = 'status-indicator danger';
            statusEl.querySelector('.status-text').textContent = 'Retrying Connection...';
            setTimeout(startSignalR, 3000);
        }
    }

    startSignalR();
});
