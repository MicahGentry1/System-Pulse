/**
 * PULSE System Monitor v2.0 - SignalR Real-Time Telemetry Client
 */

document.addEventListener('DOMContentLoaded', () => {
    window.onerror = function(message, source, lineno) {
        console.error('[Diagnostic Error]:', message, source, lineno);
        const statusText = document.querySelector('.status-text');
        if (statusText) statusText.textContent = `JS Error: L${lineno}`;
    };

    window.onunhandledrejection = function(event) {
        console.warn('[Unhandled Rejection]:', event.reason);
    };

    // Theme Switcher & Persistence
    const savedTheme = localStorage.getItem('pulse-theme') || 'cyan';
    setTheme(savedTheme);

    document.querySelectorAll('.theme-dot').forEach(dot => {
        dot.addEventListener('click', () => {
            const theme = dot.getAttribute('data-theme');
            setTheme(theme);
        });
    });

    function setTheme(theme) {
        document.body.setAttribute('data-theme', theme);
        localStorage.setItem('pulse-theme', theme);
        document.querySelectorAll('.theme-dot').forEach(d => {
            d.classList.toggle('active', d.getAttribute('data-theme') === theme);
        });
    }

    // Flush RAM Handler
    const flushRamBtn = document.getElementById('btn-flush-ram');
    flushRamBtn?.addEventListener('click', async () => {
        flushRamBtn.disabled = true;
        flushRamBtn.textContent = '🧹 Flushing...';
        try {
            const res = await fetch('/api/memory/flush', { method: 'POST' });
            const data = await res.json();
            if (data.success) {
                showToast('RAM Memory Flush', data.message, 'info');
            } else {
                showToast('RAM Flush', data.message, 'warning');
            }
        } catch (e) {
            showToast('RAM Flush Error', e.message, 'warning');
        } finally {
            flushRamBtn.disabled = false;
            flushRamBtn.textContent = '🧹 Flush RAM';
        }
    });

    // Mini-Widget Mode Toggle
    let isMiniMode = false;
    const miniBtn = document.getElementById('btn-mini-mode');
    miniBtn?.addEventListener('click', async () => {
        isMiniMode = !isMiniMode;
        const endpoint = isMiniMode ? '/api/window/mini' : '/api/window/normal';
        try {
            await fetch(endpoint, { method: 'POST' });
            miniBtn.textContent = isMiniMode ? '🗖 Normal Mode' : '🗗 Mini Mode';
        } catch (e) { }
    });

    // CPU Benchmark Handler
    const runBenchBtn = document.getElementById('btn-run-bench');
    const benchSingleEl = document.getElementById('bench-single-score');
    const benchMultiEl = document.getElementById('bench-multi-score');
    const benchStatusText = document.getElementById('bench-status-text');

    runBenchBtn?.addEventListener('click', async () => {
        runBenchBtn.disabled = true;
        runBenchBtn.textContent = '⏳ Testing...';
        benchStatusText.textContent = 'Running 4s multi-core stress benchmark...';

        try {
            const res = await fetch('/api/benchmark/run', { method: 'POST' });
            const data = await res.json();

            benchSingleEl.textContent = data.singleCoreScore.toLocaleString() + ' PTS';
            benchMultiEl.textContent = data.multiCoreScore.toLocaleString() + ' PTS';
            benchStatusText.textContent = `Tier: ${data.scoreRating} (${(data.totalOperations / 1000000).toFixed(1)}M ops)`;
            showToast('Benchmark Complete', `Multi-Core Score: ${data.multiCoreScore.toLocaleString()} PTS`, 'info');
        } catch (err) {
            benchStatusText.textContent = 'Benchmark failed: ' + err.message;
        } finally {
            runBenchBtn.disabled = false;
            runBenchBtn.textContent = '⚡ Run Benchmark';
        }
    });

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

            ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
            ctx.lineWidth = 1;
            for (let y = 0; y <= this.h; y += this.h / 4) {
                ctx.beginPath();
                ctx.moveTo(0, y);
                ctx.lineTo(this.w, y);
                ctx.stroke();
            }

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

                ctx.strokeStyle = sConf.color;
                ctx.lineWidth = 2.5;
                ctx.stroke();

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
            { color: '#00f2fe', fillColor: 'rgba(0, 242, 254, 0.15)' },
            { color: '#8a2be2', fillColor: 'rgba(138, 43, 226, 0.15)' }
        ]
    });

    // App State
    let latestProcesses = [];
    let latestConnections = [];
    let latestStartupPrograms = [];
    let activeKillPid = null;
    let activePresetFilter = 'all';
    const seenAlertKeys = new Set();

    // DOM References
    const statusEl = document.getElementById('connection-status');
    const procTableBody = document.getElementById('proc-table-body');
    const connTableBody = document.getElementById('conn-table-body');
    const startupTableBody = document.getElementById('startup-table-body');
    const startupCountBadge = document.getElementById('startup-count');
    const connSearchInput = document.getElementById('conn-search');
    const connCountBadge = document.getElementById('conn-count');
    const procSearchInput = document.getElementById('proc-search');
    const procSortSelect = document.getElementById('proc-sort');
    const procCountBadge = document.getElementById('proc-filtered-count');
    const toastContainer = document.getElementById('toast-container');

    // Export Dropdown
    const exportBtn = document.getElementById('btn-export');
    const exportMenu = document.getElementById('export-menu');

    exportBtn?.addEventListener('click', (e) => {
        e.stopPropagation();
        exportMenu?.classList.toggle('hidden');
    });

    document.addEventListener('click', () => {
        exportMenu?.classList.add('hidden');
    });

    // Preset Filter Chips
    document.querySelectorAll('.filter-chips .chip').forEach(chip => {
        chip.addEventListener('click', () => {
            document.querySelectorAll('.filter-chips .chip').forEach(c => c.classList.remove('active'));
            chip.classList.add('active');
            activePresetFilter = chip.getAttribute('data-filter');
            renderProcessTable();
        });
    });

    // Modal DOM
    const killModal = document.getElementById('kill-modal');
    const modalProcName = document.getElementById('modal-proc-name');
    const modalProcPid = document.getElementById('modal-proc-pid');
    const modalCancelBtn = document.getElementById('modal-cancel');
    const modalConfirmBtn = document.getElementById('modal-confirm');

    function formatUptime(seconds) {
        const d = Math.floor(seconds / (3600 * 24));
        const h = Math.floor((seconds % (3600 * 24)) / 3600);
        const m = Math.floor((seconds % 3600) / 60);
        const s = Math.floor(seconds % 60);
        return `${d > 0 ? d + 'd ' : ''}${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    }

    function formatSpeed(kbps) {
        if (kbps >= 1024) return (kbps / 1024).toFixed(1) + ' MB/s';
        return kbps.toFixed(0) + ' KB/s';
    }

    const getBaseUrl = () => {
        return (window.location.origin && window.location.origin.startsWith('http'))
            ? window.location.origin
            : 'http://127.0.0.1:5200';
    };

    // SignalR Connection
    let connection = null;
    if (typeof signalR !== 'undefined') {
        try {
            connection = new signalR.HubConnectionBuilder()
                .withUrl(`${getBaseUrl()}/hubs/metrics`)
                .withAutomaticReconnect([0, 1000, 2000, 5000])
                .build();

            connection.onreconnecting(() => {
                if (statusEl) {
                    statusEl.className = 'status-indicator warning';
                    statusEl.querySelector('.status-text').textContent = 'Reconnecting...';
                }
            });

            connection.onreconnected(() => {
                if (statusEl) {
                    statusEl.className = 'status-indicator';
                    statusEl.querySelector('.status-text').textContent = 'Live Streaming';
                }
            });

            connection.onclose(() => {
                if (statusEl) {
                    statusEl.className = 'status-indicator danger';
                    statusEl.querySelector('.status-text').textContent = 'Disconnected';
                }
            });
        } catch (err) {
            console.warn('[SignalR Init Warning]:', err);
        }
    }

    // Handle Telemetry Snapshots
    const handleMetricsSnapshot = (snapshot) => {
        if (!snapshot) return;

        if (statusEl) {
            statusEl.className = 'status-indicator';
            statusEl.querySelector('.status-text').textContent = 'Live Streaming';
        }

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

        // 3b. GPU Telemetry
        if (snapshot.gpu) {
            const gpuNameEl = document.getElementById('gpu-name');
            const gpuDriverEl = document.getElementById('gpu-driver-ver');
            const gpuPctEl = document.getElementById('gpu-vram-pct');
            const vramUsedEl = document.getElementById('vram-used');
            const vramTotalEl = document.getElementById('vram-total');
            const vramFillEl = document.getElementById('vram-progress-fill');

            if (gpuNameEl) gpuNameEl.textContent = snapshot.gpu.name || 'Integrated Graphics';
            if (gpuDriverEl) gpuDriverEl.textContent = snapshot.gpu.driverVersion || 'N/A';
            if (gpuPctEl) gpuPctEl.textContent = (snapshot.gpu.vramUsagePercentage || 0).toFixed(1) + '%';
            if (vramUsedEl) vramUsedEl.textContent = (snapshot.gpu.vramUsedMb || 0).toFixed(0) + ' MB';
            if (vramTotalEl) vramTotalEl.textContent = (snapshot.gpu.vramTotalMb || 0).toFixed(0) + ' MB';
            if (vramFillEl) vramFillEl.style.width = Math.min(100, Math.max(0, snapshot.gpu.vramUsagePercentage || 0)) + '%';
        }

        // 3c. Ping Latency
        if (snapshot.pingLatency) {
            const pingMsEl = document.getElementById('ping-ms-text');
            const pingHostEl = document.getElementById('ping-target-host');
            const pingStatusEl = document.getElementById('ping-status-text');

            if (pingMsEl) pingMsEl.textContent = (snapshot.pingLatency.pingMs || 0) + ' ms';
            if (pingHostEl) pingHostEl.textContent = 'Target: ' + (snapshot.pingLatency.targetHost || '1.1.1.1');
            if (pingStatusEl) pingStatusEl.textContent = snapshot.pingLatency.status || 'Optimal';
        }

        // 4. Power & Battery
        if (snapshot.power) {
            const p = snapshot.power;
            document.getElementById('power-pct-text').textContent = p.hasBattery ? `${p.batteryLifePercent}%` : 'AC';
            document.getElementById('power-status-label').textContent = p.powerStatusText || 'AC Power';
            document.getElementById('power-icon').textContent = p.isCharging ? '⚡' : p.isAcOnline ? '🔌' : '🔋';
            document.getElementById('battery-bar-fill').style.width = p.hasBattery ? `${p.batteryLifePercent}%` : '100%';
        }

        // 5. Storage Drives
        const disks = snapshot.disks || snapshot.Disks || [];
        if (disks.length > 0) {
            updateDrives(disks);
        }

        // 6. Network Metrics
        const netIfaces = snapshot.networkInterfaces || snapshot.NetworkInterfaces || [];
        if (netIfaces.length > 0) {
            let totalDown = 0;
            let totalUp = 0;
            netIfaces.forEach(ni => {
                totalDown += ni.downloadSpeedKbps || ni.DownloadSpeedKbps || 0;
                totalUp += ni.uploadSpeedKbps || ni.UploadSpeedKbps || 0;
            });

            document.getElementById('net-down-speed').textContent = formatSpeed(totalDown);
            document.getElementById('net-up-speed').textContent = formatSpeed(totalUp);

            netChart.pushValues([totalDown, totalUp]);
            updateNetworkInterfaces(netIfaces);
        }

        // 7. Active Sockets
        const conns = snapshot.activeConnections || snapshot.ActiveConnections || [];
        if (conns.length > 0) {
            latestConnections = conns;
            renderConnectionsTable();
        }

        // 8. Startup Programs
        const startProcs = snapshot.startupPrograms || snapshot.StartupPrograms || [];
        if (startProcs.length > 0) {
            latestStartupPrograms = startProcs;
            renderStartupTable();
        }

        // 9. Benchmark Update
        const bench = snapshot.latestBenchmark || snapshot.LatestBenchmark;
        if (bench && !bench.isRunning && benchSingleEl && benchMultiEl) {
            benchSingleEl.textContent = (bench.singleCoreScore || bench.SingleCoreScore || 0).toLocaleString() + ' PTS';
            benchMultiEl.textContent = (bench.multiCoreScore || bench.MultiCoreScore || 0).toLocaleString() + ' PTS';
            benchStatusText.textContent = `Tier: ${bench.scoreRating || bench.ScoreRating || 'Tested'}`;
        }

        // 10. Alerts
        const alerts = snapshot.alerts || snapshot.Alerts || [];
        if (alerts.length > 0) {
            alerts.forEach(alert => {
                const key = `${alert.title || alert.Title}:${alert.message || alert.Message}`;
                if (!seenAlertKeys.has(key)) {
                    seenAlertKeys.add(key);
                    showToast(alert.title || alert.Title, alert.message || alert.Message, (alert.type || alert.Type || 'info').toLowerCase());
                    setTimeout(() => seenAlertKeys.delete(key), 30000);
                }
            });
        }

        // 11. Processes
        const procs = snapshot.processes || snapshot.Processes || [];
        if (procs.length > 0) {
            latestProcesses = procs;
            renderProcessTable();
        }
    };

    if (connection) {
        connection.on('ReceiveMetrics', handleMetricsSnapshot);
    }
    document.addEventListener('InitialSnapshot', (e) => handleMetricsSnapshot(e.detail));

    function showToast(title, message, type = 'info') {
        if (!toastContainer) return;
        const toast = document.createElement('div');
        toast.className = `toast ${type}`;
        toast.innerHTML = `
            <div>
                <strong>${escapeHtml(title)}</strong>
                <div>${escapeHtml(message)}</div>
            </div>
        `;
        toastContainer.appendChild(toast);
        setTimeout(() => {
            toast.style.opacity = '0';
            setTimeout(() => toast.remove(), 300);
        }, 5000);
    }

    function updateCoreMatrix(coreUsages) {
        const matrixContainer = document.getElementById('core-matrix');
        if (!matrixContainer) return;

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
             function updateDrives(disks) {
        const listEl = document.getElementById('drives-list');
        if (!listEl) return;

        listEl.innerHTML = disks.map(drive => {
            const vLabel = drive.volumeLabel || drive.VolumeLabel || 'Local Disk';
            const dName = drive.name || drive.Name || 'C:\\';
            const dFormat = drive.driveFormat || drive.DriveFormat || 'NTFS';
            const dType = drive.driveType || drive.DriveType || 'Fixed';
            const usedG = drive.usedGb ?? drive.UsedGb ?? 0;
            const totalG = drive.totalGb ?? drive.TotalGb ?? 1;
            const pct = drive.usagePercentage ?? drive.UsagePercentage ?? 0;

            return `
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
                            <div class="title">${vLabel} (${dName})</div>
                            <div class="subtitle">${dFormat} &bull; ${dType}</div>
                        </div>
                    </div>
                    <div class="drive-usage-text">${usedG} GB / ${totalG} GB</div>
                </div>
                <div class="drive-bar-bg">
                    <div class="drive-bar-fill" style="width: ${Math.min(100, pct)}%"></div>
                </div>
            </div>
        `;
        }).join('');
    }

    function updateNetworkInterfaces(ifaces) {
        const listEl = document.getElementById('net-interfaces-list');
        if (!listEl) return;

        listEl.innerHTML = ifaces.map(ni => {
            const name = ni.name || ni.Name || 'Network Interface';
            const ip = ni.ipAddress || ni.IpAddress || 'No IPv4';
            const down = ni.downloadSpeedKbps ?? ni.DownloadSpeedKbps ?? 0;
            const up = ni.uploadSpeedKbps ?? ni.UploadSpeedKbps ?? 0;
            return `
            <div class="net-iface-row">
                <span><strong>${name}</strong> (${ip})</span>
                <span>↓ ${formatSpeed(down)} | ↑ ${formatSpeed(up)}</span>
            </div>
        `;
        }).join('');
    }

    function renderStartupTable() {
        if (!startupTableBody) return;
        if (startupCountBadge) startupCountBadge.textContent = `${latestStartupPrograms.length} programs`;

        startupTableBody.innerHTML = latestStartupPrograms.map(sp => {
            const name = sp.name || sp.Name || 'Program';
            const cmd = sp.command || sp.Command || '';
            const loc = sp.location || sp.Location || 'Startup';
            return `
            <tr>
                <td><strong class="proc-name">${escapeHtml(name)}</strong></td>
                <td><span class="proc-pid">${escapeHtml(cmd)}</span></td>
                <td>${escapeHtml(loc)}</td>
                <td><span class="speed-badge down">Enabled</span></td>
            </tr>
        `;
        }).join('');
    }

    function renderConnectionsTable() {
        if (!connTableBody) return;
        const q = (connSearchInput?.value || '').toLowerCase().trim();

        const filtered = latestConnections.filter(c => {
            const local = (c.localEndPoint || c.LocalEndPoint || '').toLowerCase();
            const remote = (c.remoteEndPoint || c.RemoteEndPoint || '').toLowerCase();
            const state = (c.state || c.State || '').toLowerCase();
            const port = (c.port ?? c.Port ?? 0).toString();
            return local.includes(q) || remote.includes(q) || state.includes(q) || port.includes(q);
        });

        if (connCountBadge) connCountBadge.textContent = `${filtered.length} active`;

        connTableBody.innerHTML = filtered.map(c => {
            const proto = c.protocol || c.Protocol || 'TCP';
            const local = c.localEndPoint || c.LocalEndPoint || '';
            const remote = c.remoteEndPoint || c.RemoteEndPoint || '';
            const port = c.port ?? c.Port ?? 0;
            const state = c.state || c.State || 'Listen';
            return `
            <tr>
                <td><span class="speed-badge down">${proto}</span></td>
                <td><strong class="proc-pid">${escapeHtml(local)}</strong></td>
                <td>${escapeHtml(remote)}</td>
                <td><span class="proc-name">${port}</span></td>
                <td>${escapeHtml(state)}</td>
            </tr>
        `;
        }).join('');
    }

    connSearchInput?.addEventListener('input', renderConnectionsTable);

    function renderProcessTable() {
        if (!procTableBody) return;

        const query = (procSearchInput.value || '').toLowerCase().trim();
        const sortBy = procSortSelect.value || 'ram';

        let filtered = latestProcesses.filter(p => {
            const name = (p.name || p.Name || '').toLowerCase();
            const pid = (p.pid ?? p.Pid ?? 0).toString();
            return name.includes(query) || pid.includes(query);
        });

        if (activePresetFilter === 'cpu') {
            filtered = filtered.filter(p => (p.cpuPercentage ?? p.CpuPercentage ?? 0) >= 2.0);
        } else if (activePresetFilter === 'ram') {
            filtered = filtered.filter(p => (p.workingSetMb ?? p.WorkingSetMb ?? 0) >= 150.0);
        }

        filtered.sort((a, b) => {
            const ramA = a.workingSetMb ?? a.WorkingSetMb ?? 0;
            const ramB = b.workingSetMb ?? b.WorkingSetMb ?? 0;
            const cpuA = a.cpuPercentage ?? a.CpuPercentage ?? 0;
            const cpuB = b.cpuPercentage ?? b.CpuPercentage ?? 0;
            const nameA = a.name || a.Name || '';
            const nameB = b.name || b.Name || '';
            const pidA = a.pid ?? a.Pid ?? 0;
            const pidB = b.pid ?? b.Pid ?? 0;

            if (sortBy === 'ram') return ramB - ramA;
            if (sortBy === 'cpu') return cpuB - cpuA;
            if (sortBy === 'name') return nameA.localeCompare(nameB);
            if (sortBy === 'pid') return pidA - pidB;
            return 0;
        });

        if (procCountBadge) procCountBadge.textContent = `${filtered.length} processes`;
        const maxRam = Math.max(...filtered.map(p => p.workingSetMb ?? p.WorkingSetMb ?? 0), 1);

        procTableBody.innerHTML = filtered.map(p => {
            const pid = p.pid ?? p.Pid ?? 0;
            const name = p.name || p.Name || 'Process';
            const ram = p.workingSetMb ?? p.WorkingSetMb ?? 0;
            const cpu = p.cpuPercentage ?? p.CpuPercentage ?? 0;
            const threads = p.threadCount ?? p.ThreadCount ?? 1;
            const priority = p.priorityClass || p.PriorityClass || 'Normal';

            return `
            <tr>
                <td class="proc-pid">${pid}</td>
                <td class="proc-name">${escapeHtml(name)}</td>
                <td>
                    <div class="proc-mem-bar">
                        <span class="proc-mem-val">${ram} MB</span>
                        <div class="proc-mem-mini-bg">
                            <div class="proc-mem-mini-fill" style="width: ${Math.min(100, (ram / maxRam) * 100)}%"></div>
                        </div>
                    </div>
                </td>
                <td>${cpu.toFixed(1)}%</td>
                <td>${threads}</td>
                <td>
                    <select class="priority-select" data-pid="${pid}">
                        <option value="Idle" ${priority === 'Idle' ? 'selected' : ''}>Idle</option>
                        <option value="BelowNormal" ${priority === 'BelowNormal' ? 'selected' : ''}>Below Normal</option>
                        <option value="Normal" ${priority === 'Normal' ? 'selected' : ''}>Normal</option>
                        <option value="AboveNormal" ${priority === 'AboveNormal' ? 'selected' : ''}>Above Normal</option>
                        <option value="High" ${priority === 'High' ? 'selected' : ''}>High</option>
                    </select>
                </td>
                <td>
                    <button class="btn-kill" data-pid="${pid}" data-name="${escapeHtml(name)}">End Task</button>
                </td>
            </tr>
        `;
        }).join('');

        procTableBody.querySelectorAll('.btn-kill').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const pid = parseInt(e.target.getAttribute('data-pid'));
                const name = e.target.getAttribute('data-name');
                openKillModal(pid, name);
            });
        });

        procTableBody.querySelectorAll('.priority-select').forEach(sel => {
            sel.addEventListener('change', async (e) => {
                const pid = parseInt(e.target.getAttribute('data-pid'));
                const newPriority = e.target.value;
                try {
                    const res = await fetch(`/api/process/${pid}/priority`, {
                        method: 'POST',
                        headers: { 'Content-Type': 'application/json' },
                        body: JSON.stringify({ priority: newPriority })
                    });
                    const data = await res.json();
                    if (data.success) {
                        showToast('Priority Updated', data.message, 'info');
                    } else {
                        showToast('Priority Error', data.message, 'warning');
                    }
                } catch (err) {
                    showToast('Priority Error', err.message, 'warning');
                }
            });
        });
    }

    function escapeHtml(str) {
        return str.replace(/[&<>'"]/g, 
            tag => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;' }[tag] || tag)
        );
    }

    procSearchInput?.addEventListener('input', renderProcessTable);
    procSortSelect?.addEventListener('change', renderProcessTable);

    function openKillModal(pid, name) {
        activeKillPid = pid;
        if (modalProcPid) modalProcPid.textContent = pid;
        if (modalProcName) modalProcName.textContent = name;
        killModal?.classList.remove('hidden');
    }

    function closeKillModal() {
        activeKillPid = null;
        killModal?.classList.add('hidden');
    }

    modalCancelBtn?.addEventListener('click', closeKillModal);

    modalConfirmBtn?.addEventListener('click', async () => {
        if (!activeKillPid) return;
        const pidToKill = activeKillPid;
        closeKillModal();

        try {
            const result = await connection.invoke("KillProcess", pidToKill);
            if (result && result.success) {
                latestProcesses = latestProcesses.filter(p => (p.pid ?? p.Pid) !== pidToKill);
                renderProcessTable();
                showToast('Task Ended', result.message, 'info');
            } else {
                showToast('Kill Failed', result?.message || 'Failed to kill process', 'warning');
            }
        } catch (err) {
            fetch(`/api/process/${pidToKill}`, { method: 'DELETE' })
                .then(res => res.json())
                .then(data => {
                    if (data.success) {
                        latestProcesses = latestProcesses.filter(p => (p.pid ?? p.Pid) !== pidToKill);
                        renderProcessTable();
                        showToast('Task Ended', data.message, 'info');
                    } else {
                        showToast('Kill Failed', data.message, 'warning');
                    }
                })
                .catch(e => showToast('Kill Error', e.message, 'warning'));
        }
    });

    async function fetchRestSnapshot() {
        try {
            const baseUrl = (window.location.origin && window.location.origin.startsWith('http'))
                ? window.location.origin
                : 'http://127.0.0.1:5200';
            const res = await fetch(`${baseUrl}/api/system/snapshot`);
            if (res.ok) {
                const snapshot = await res.json();
                handleMetricsSnapshot(snapshot);
            }
        } catch (err) {
            console.warn('[REST Fetch Error]:', err);
        }
    }

    async function startSignalR() {
        if (!connection) return;
        try {
            await connection.start();
            if (statusEl) {
                statusEl.className = 'status-indicator';
                statusEl.querySelector('.status-text').textContent = 'Live Streaming';
            }
        } catch (err) {
            console.warn('[SignalR Error]:', err);
            if (statusEl) {
                statusEl.className = 'status-indicator';
                statusEl.querySelector('.status-text').textContent = 'Live (REST Mode)';
            }
            setTimeout(startSignalR, 4000);
        }
    }

    fetchRestSnapshot();
    setInterval(fetchRestSnapshot, 1000);
    startSignalR();
});
