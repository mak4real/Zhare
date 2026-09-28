// HotspotShare PC Web Client
document.addEventListener('DOMContentLoaded', () => {
    initTabs();
    initFilesList();
    initFileUpload();
    initClipboard();
    initDevicePolling();
});

// Tab Switching
function initTabs() {
    const tabButtons = document.querySelectorAll('.tab-btn');
    const tabPanes = document.querySelectorAll('.tab-pane');

    tabButtons.forEach(btn => {
        btn.addEventListener('click', () => {
            const targetId = btn.getAttribute('data-tab');

            tabButtons.forEach(b => b.classList.remove('active'));
            tabPanes.forEach(p => p.classList.remove('active'));

            btn.classList.add('active');
            const targetPane = document.getElementById(targetId);
            if (targetPane) targetPane.classList.add('active');
        });
    });
}

// Format bytes into readable string (KB, MB, GB)
function formatBytes(bytes, decimals = 2) {
    if (!bytes || bytes === 0) return '0 Bytes';
    const k = 1024;
    const dm = decimals < 0 ? 0 : decimals;
    const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
}

// Get icon by file name/mime
function getFileIcon(filename) {
    const ext = filename.split('.').pop().toLowerCase();
    if (['jpg', 'jpeg', 'png', 'gif', 'webp', 'svg'].includes(ext)) return '🖼️';
    if (['mp4', 'mkv', 'avi', 'mov', 'webm'].includes(ext)) return '🎬';
    if (['mp3', 'wav', 'flac', 'aac', 'ogg', 'm4a'].includes(ext)) return '🎵';
    if (['pdf'].includes(ext)) return '📕';
    if (['zip', 'rar', '7z', 'tar', 'gz'].includes(ext)) return '📦';
    if (['doc', 'docx', 'txt', 'rtf'].includes(ext)) return '📄';
    if (['apk'].includes(ext)) return '🤖';
    if (['exe', 'msi'].includes(ext)) return '💻';
    return '📁';
}

// Fetch & Display Shared Files
let currentFiles = [];
function fetchSharedFiles() {
    fetch('/api/files')
        .then(res => res.json())
        .then(files => {
            currentFiles = files;
            renderFiles(files);
            document.getElementById('download-count').innerText = files.length;
        })
        .catch(err => {
            console.error('Error fetching shared files:', err);
        });
}

function renderFiles(files) {
    const emptyState = document.getElementById('empty-files-view');
    const fileGrid = document.getElementById('file-grid');

    if (!files || files.length === 0) {
        emptyState.style.display = 'flex';
        fileGrid.style.display = 'none';
        return;
    }

    emptyState.style.display = 'none';
    fileGrid.style.display = 'grid';
    fileGrid.innerHTML = '';

    files.forEach((file, index) => {
        const card = document.createElement('div');
        card.className = 'file-card';
        card.innerHTML = `
            <div class="file-card-info">
                <div class="file-icon">${getFileIcon(file.name)}</div>
                <div class="file-details">
                    <div class="file-name" title="${file.name}">${file.name}</div>
                    <div class="file-meta">${formatBytes(file.size)}</div>
                </div>
            </div>
            <a href="/api/download/${index}" class="btn btn-primary" download="${file.name}">
                ⬇️ Download
            </a>
        `;
        fileGrid.appendChild(card);
    });
}

function initFilesList() {
    document.getElementById('refresh-files-btn').addEventListener('click', fetchSharedFiles);
    fetchSharedFiles();
    // Auto-refresh list every 5 seconds
    setInterval(fetchSharedFiles, 5000);
}

// File Upload with Drag & Drop & Real-time Progress Tracking
function initFileUpload() {
    const dropZone = document.getElementById('drop-zone');
    const fileInput = document.getElementById('file-input');

    ['dragenter', 'dragover'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.add('dragover');
        });
    });

    ['dragleave', 'drop'].forEach(eventName => {
        dropZone.addEventListener(eventName, (e) => {
            e.preventDefault();
            e.stopPropagation();
            dropZone.classList.remove('dragover');
        });
    });

    dropZone.addEventListener('drop', (e) => {
        const dt = e.dataTransfer;
        const files = dt.files;
        if (files.length > 0) {
            handleFileUploads(files);
        }
    });

    fileInput.addEventListener('change', (e) => {
        if (fileInput.files.length > 0) {
            handleFileUploads(fileInput.files);
            fileInput.value = '';
        }
    });
}

function handleFileUploads(files) {
    const progressArea = document.getElementById('upload-progress-area');
    const progressItems = document.getElementById('progress-items');
    progressArea.style.display = 'block';

    Array.from(files).forEach(file => {
        uploadSingleFile(file, progressItems);
    });
}

function uploadSingleFile(file, container) {
    const item = document.createElement('div');
    item.className = 'progress-item';
    const itemId = 'upload-' + Math.random().toString(36).substr(2, 9);
    item.id = itemId;

    item.innerHTML = `
        <div class="progress-header">
            <span>${file.name} (${formatBytes(file.size)})</span>
            <span class="progress-pct" id="${itemId}-pct">0%</span>
        </div>
        <div class="progress-bar-bg">
            <div class="progress-bar-fill" id="${itemId}-bar"></div>
        </div>
        <div class="progress-stats">
            <span id="${itemId}-speed">Connecting...</span>
            <span id="${itemId}-status">Uploading</span>
        </div>
    `;
    container.prepend(item);

    const xhr = new XMLHttpRequest();
    const formData = new FormData();
    formData.append('file', file, file.name);

    let startTime = Date.now();
    let prevLoaded = 0;
    let prevTime = startTime;

    xhr.upload.addEventListener('progress', (e) => {
        if (e.lengthComputable) {
            const now = Date.now();
            const percent = Math.round((e.loaded / e.total) * 100);
            
            // Calculate speed
            const timeDiff = (now - prevTime) / 1000;
            if (timeDiff >= 0.5) {
                const bytesDiff = e.loaded - prevLoaded;
                const speedBps = bytesDiff / timeDiff;
                document.getElementById(`${itemId}-speed`).innerText = `${formatBytes(speedBps)}/s`;
                prevTime = now;
                prevLoaded = e.loaded;
            }

            document.getElementById(`${itemId}-pct`).innerText = percent + '%';
            document.getElementById(`${itemId}-bar`).style.width = percent + '%';
        }
    });

    xhr.addEventListener('load', () => {
        if (xhr.status >= 200 && xhr.status < 300) {
            document.getElementById(`${itemId}-pct`).innerText = '100%';
            document.getElementById(`${itemId}-bar`).style.width = '100%';
            document.getElementById(`${itemId}-status`).innerText = '✅ Saved on Phone';
            document.getElementById(`${itemId}-status`).style.color = '#3fb950';
            document.getElementById(`${itemId}-speed`).innerText = 'Complete';
        } else {
            document.getElementById(`${itemId}-status`).innerText = '❌ Error ' + xhr.status;
            document.getElementById(`${itemId}-status`).style.color = '#da3633';
        }
    });

    xhr.addEventListener('error', () => {
        document.getElementById(`${itemId}-status`).innerText = '❌ Network Error';
        document.getElementById(`${itemId}-status`).style.color = '#da3633';
    });

    xhr.open('POST', '/api/upload', true);
    xhr.send(formData);
}

// Clipboard Sync
function initClipboard() {
    const textarea = document.getElementById('clipboard-text');
    const sendBtn = document.getElementById('send-clipboard-btn');
    const copyBtn = document.getElementById('copy-clipboard-btn');

    // Fetch initial clipboard
    fetch('/api/clipboard')
        .then(res => res.text())
        .then(text => {
            if (text) textarea.value = text;
        })
        .catch(() => {});

    sendBtn.addEventListener('click', () => {
        const text = textarea.value;
        fetch('/api/clipboard', {
            method: 'POST',
            body: text
        }).then(res => {
            if (res.ok) {
                sendBtn.innerText = '✅ Sent!';
                setTimeout(() => { sendBtn.innerText = '🚀 Send to Phone'; }, 2000);
            }
        });
    });

    copyBtn.addEventListener('click', () => {
        navigator.clipboard.writeText(textarea.value).then(() => {
            copyBtn.innerText = '✅ Copied!';
            setTimeout(() => { copyBtn.innerText = '📋 Copy to PC Clipboard'; }, 2000);
        });
    });
}

// Device Status Polling
function initDevicePolling() {
    function checkDevice() {
        fetch('/api/info')
            .then(res => res.json())
            .then(info => {
                if (info && info.device) {
                    document.getElementById('device-subtitle').innerText = `Connected to ${info.device}`;
                }
            })
            .catch(() => {});
    }
    checkDevice();
    setInterval(checkDevice, 10000);
}
