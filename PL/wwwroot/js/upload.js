/* =====================================================================
 * upload.js — shared multi-file upload helper for the LMS AI Ra app.
 *
 * Used by:
 *   - /Pages/Lecturer/Portal.cshtml  (single-subject context)
 *   - /Pages/Subject/Details.cshtml  (single-subject context)
 *
 * Exposes a global `LmsUpload` namespace with helpers:
 *   - bindDropzone(opts)        : wires drag-drop + file-input, opens a per-file list
 *   - uploadOneFile(...)         : XHR upload for a single File, returns a Promise
 *   - startUploadQueue(...)      : parallel upload with a concurrency cap
 *   - pollDocumentStatus(...)    : 3s polling for doc status updates
 *
 * Visual rules:
 *   - Block the upload card while uploads are in flight.
 *   - Per-file progress bar with status text (Queued / Uploading / Processing / Done / Failed).
 *   - Allow up to 3 concurrent uploads.
 * ===================================================================== */
(function (global) {
    'use strict';

    const MAX_CONCURRENT = 3;

    /**
     * Render an upload overlay (one row per file) into the given container.
     * @param {HTMLElement} card     Card wrapper used for block-UI.
     * @param {File[]} files
     * @returns {{ rowFor: (file) => HTMLElement, allRows: HTMLElement[] }}
     */
    function renderUploadList(card, files) {
        card.classList.add('relative', 'pointer-events-none', 'opacity-95');

        // Prevent re-triggering upload while in-flight
        const fi = card.querySelector('input[type="file"]');
        if (fi) fi.disabled = true;
        const dz = card.querySelector('[id$="Dropzone"],[id$="dropzone"],[id$="DropZone"]');
        if (dz) dz.style.pointerEvents = 'none';

        let overlay = card.querySelector('.upload-overlay');
        if (!overlay) {
            overlay = document.createElement('div');
            overlay.className = 'upload-overlay absolute inset-0 z-30 bg-background/85 backdrop-blur-sm rounded-2xl flex flex-col p-4 overflow-auto';
            card.appendChild(overlay);
        }
        overlay.innerHTML = `
            <div class="flex items-center justify-between mb-3 select-none">
                <div class="flex items-center gap-2">
                    <span class="w-5 h-5 border-2 border-t-transparent border-primary rounded-full animate-spin"></span>
                    <h4 class="font-bold text-on-surface text-sm">Đang tải lên <span class="upload-overlay-count">${files.length}</span> tệp...</h4>
                </div>
                <span class="text-[11px] text-on-surface-variant upload-overlay-summary">0/${files.length} hoàn tất</span>
            </div>
            <div class="space-y-2 upload-overlay-list"></div>
        `;
        const list = overlay.querySelector('.upload-overlay-list');
        const rows = files.map((f, idx) => {
            const row = document.createElement('div');
            row.className = 'bg-surface border border-outline-variant/40 rounded-xl p-3 flex items-center gap-3 shadow-sm';
            row.innerHTML = `
                <span class="material-symbols-outlined text-[22px] text-primary filled">description</span>
                <div class="min-w-0 flex-1">
                    <p class="text-xs font-semibold text-on-surface truncate" title="${escapeHtml(f.name)}">${escapeHtml(f.name)}</p>
                    <p class="text-[10px] text-outline">${formatBytes(f.size)}</p>
                    <div class="w-full bg-surface-container-high rounded-full h-1.5 mt-1.5 overflow-hidden">
                        <div class="bg-primary h-1.5 rounded-full transition-all" style="width: 0%"></div>
                    </div>
                    <p class="text-[10px] text-on-surface-variant mt-1 row-status">Đang chờ...</p>
                </div>
                <span class="material-symbols-outlined text-[20px] text-outline row-icon">pending</span>
            `;
            list.appendChild(row);
            return { file: f, row, idx, done: false, success: false };
        });
        function updateSummary() {
            const done = rows.filter(r => r.done).length;
            const fail = rows.filter(r => r.done && !r.success).length;
            overlay.querySelector('.upload-overlay-summary').textContent = `${done}/${files.length} hoàn tất` + (fail ? ` (${fail} lỗi)` : '');
        }
        return { rows, updateSummary };
    }

    /**
     * Update a row's progress bar and status text.
     */
    function setRowState(rowHandle, progressPct, statusText, statusKind /* 'pending'|'uploading'|'processing'|'success'|'failed' */) {
        const bar = rowHandle.row.querySelector('.bg-primary');
        const text = rowHandle.row.querySelector('.row-status');
        const icon = rowHandle.row.querySelector('.row-icon');
        if (bar && progressPct != null) bar.style.width = progressPct + '%';
        if (text) text.textContent = statusText;
        if (icon && statusKind) {
            icon.textContent = statusKind === 'success' ? 'check_circle'
                : statusKind === 'failed' ? 'error'
                : statusKind === 'processing' ? 'hourglass_top'
                : statusKind === 'uploading' ? 'cloud_upload' : 'pending';
            icon.classList.remove('text-outline', 'text-primary', 'text-tertiary', 'text-error');
            icon.classList.add(statusKind === 'success' ? 'text-tertiary'
                : statusKind === 'failed' ? 'text-error'
                : 'text-primary');
        }
    }

    /**
     * Upload a single file via XHR. The caller receives per-percent callbacks.
     * Resolves to { success, message, status }.
     *
     * @param {Object} opts
     * @param {File} opts.file
     * @param {string} opts.url          e.g. '?handler=Upload'
     * @param {string} opts.token        antiforgery token
     * @param {string} [opts.subjectId]
     * @param {string} [opts.fieldName='file'] — form field name for the file
     * @param {(pct:number) => void} [opts.onProgress]
     */
    function uploadOneFile(opts) {
        return new Promise((resolve) => {
            const fd = new FormData();
            fd.append(opts.fieldName || 'file', opts.file);
            if (opts.subjectId) fd.append('subjectId', opts.subjectId);
            if (opts.token) fd.append('__RequestVerificationToken', opts.token);
            if (opts.extra) {
                for (const k in opts.extra) fd.append(k, opts.extra[k]);
            }
            const xhr = new XMLHttpRequest();
            xhr.open('POST', opts.url);
            if (opts.token) xhr.setRequestHeader('RequestVerificationToken', opts.token);
            xhr.upload.addEventListener('progress', e => {
                if (e.lengthComputable && opts.onProgress) {
                    opts.onProgress(Math.round((e.loaded / e.total) * 100));
                }
            });
            xhr.onload = () => {
                let success = false, message = '', status = 'Failed';
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        const j = JSON.parse(xhr.responseText);
                        success = !!j.success;
                        message = j.message || '';
                        status = j.status || (success ? 'Success' : 'Failed');
                    } catch {
                        message = 'Phản hồi không hợp lệ';
                    }
                } else {
                    message = `Lỗi server (${xhr.status})`;
                }
                resolve({ success, message, status, raw: xhr.responseText, httpStatus: xhr.status });
            };
            xhr.onerror = () => resolve({ success: false, message: 'Lỗi mạng', status: 'Failed' });
            xhr.send(fd);
        });
    }

    /**
     * Run uploads in parallel (capped at MAX_CONCURRENT).
     * @param {Object[]} rowHandles
     * @param {Object} uploadOpts — same shape as uploadOneFile() minus `file`/`onProgress`
     */
    async function startUploadQueue(rowHandles, uploadOpts) {
        let next = 0;
        const inflight = new Set();
        async function worker() {
            while (true) {
                const i = next++;
                if (i >= rowHandles.length) break;
                const handle = rowHandles[i];
                setRowState(handle, 0, 'Đang tải lên...', 'uploading');
                const result = await uploadOneFile({
                    ...uploadOpts,
                    file: handle.file,
                    onProgress: (pct) => setRowState(handle, pct, `Đang tải lên... ${pct}%`, 'uploading')
                });
                handle.done = true;
                handle.success = result.success;
                if (result.success) {
                    setRowState(handle, 100, 'Đang xử lý AI...', 'processing');
                } else {
                    setRowState(handle, 100, `Thất bại: ${result.message || 'lỗi'}`, 'failed');
                }
            }
        }
        for (let k = 0; k < Math.min(MAX_CONCURRENT, rowHandles.length); k++) inflight.add(worker());
        await Promise.all([...inflight]);
    }

    /**
     * Bind drag-drop + file-input to start an upload flow.
     * @param {Object} opts
     * @param {string} opts.dropzoneId
     * @param {string} opts.fileInputId
     * @param {string} opts.cardSelector     CSS selector for the card to block during upload
     * @param {string} opts.uploadUrl        e.g. '?handler=Upload' or '?handler=UploadLecturerFile'
     * @param {string} opts.token            antiforgery token
     * @param {string} [opts.subjectId]
     * @param {string} [opts.fieldName='file']
     * @param {Object} [opts.extra]          extra form fields
     * @param {() => void} [opts.onAllDone]  invoked after all uploads finish (success or fail)
     */
    function bindDropzone(opts) {
        const dz = document.getElementById(opts.dropzoneId);
        const fi = document.getElementById(opts.fileInputId);
        const card = dz ? dz.closest(opts.cardSelector) : null;
        if (!dz || !fi) return;

        dz.addEventListener('click', () => fi.click());
        ['dragenter', 'dragover'].forEach(e => dz.addEventListener(e, ev => { ev.preventDefault(); dz.classList.add('border-primary'); }));
        ['dragleave', 'drop'].forEach(e => dz.addEventListener(e, ev => { ev.preventDefault(); dz.classList.remove('border-primary'); }));
        dz.addEventListener('drop', ev => {
            if (ev.dataTransfer && ev.dataTransfer.files && ev.dataTransfer.files.length) {
                handleFiles(ev.dataTransfer.files);
            }
        });
        fi.addEventListener('change', ev => {
            if (ev.target.files && ev.target.files.length) {
                handleFiles(ev.target.files);
                ev.target.value = '';
            }
        });

        async function handleFiles(fileList) {
            const files = Array.from(fileList);
            if (!files.length || !card) return;
            const { rows, updateSummary } = renderUploadList(card, files);
            const startedAt = Date.now();
            // Tick summary every 500ms while running
            const tick = setInterval(updateSummary, 500);
            try {
                await startUploadQueue(rows, {
                    url: opts.uploadUrl,
                    token: opts.token,
                    subjectId: opts.subjectId,
                    fieldName: opts.fieldName,
                    extra: opts.extra
                });
            } finally {
                clearInterval(tick);
                updateSummary();
            }
            // Show "Done" overlay for 1.2s then reload so user sees fresh list.
            const successCount = rows.filter(r => r.success).length;
            const failCount = rows.length - successCount;
            // Re-enable dropzone/fileInput
            const fi2 = card.querySelector('input[type="file"]');
            if (fi2) fi2.disabled = false;
            const dz2 = card.querySelector('[id$="Dropzone"],[id$="dropzone"],[id$="DropZone"]');
            if (dz2) dz2.style.pointerEvents = '';

            const overlay = card.querySelector('.upload-overlay');
            if (overlay) {
                // Stop spinner
                const spinner = overlay.querySelector('.animate-spin');
                if (spinner) spinner.classList.remove('animate-spin', 'border-t-transparent');

                if (failCount > 0) {
                    const header = overlay.querySelector('.select-none');
                    if (header) {
                        header.innerHTML = `
                            <div class="flex items-center gap-2">
                                <span class="material-symbols-outlined text-error">error</span>
                                <h4 class="font-bold text-on-surface text-sm">Tải lên hoàn tất với ${failCount} lỗi</h4>
                            </div>
                            <button type="button" class="bg-primary hover:bg-primary/95 text-on-primary font-bold text-xs px-3 py-1.5 rounded-lg shadow-sm transition-all" onclick="this.closest('.upload-overlay').remove(); location.reload();">Đóng</button>
                        `;
                    }
                } else {
                    setTimeout(() => {
                        if (typeof opts.onAllDone === 'function') opts.onAllDone({ successCount, failCount });
                        else location.reload();
                    }, 1200);
                }
            }
        }
    }

    /**
     * Poll every 3s for status updates on .doc-status elements.
     * Each <span data-doc-id="..."> gets refreshed; on terminal status, reloads page.
     */
    function pollDocumentStatus(handlerUrl, token) {
        const statusMap = {
            'Success': 'Thành công',
            'Failed': 'Thất bại',
            'Processing': 'Đang xử lý',
            'Pending': 'Đang chờ'
        };

        setInterval(async () => {
            const els = document.querySelectorAll('.doc-status');
            for (const el of els) {
                const id = el.dataset.docId;
                if (!id) continue;
                try {
                    const r = await fetch(`${handlerUrl}?docId=${encodeURIComponent(id)}`, {
                        headers: { 'RequestVerificationToken': token }
                    });
                    if (!r.ok) continue;
                    const j = await r.json();
                    const txt = el.querySelector('.doc-status-text');
                    const vietnameseStatus = statusMap[j.status] || j.status;

                    if (txt && txt.textContent.trim() !== vietnameseStatus) {
                        txt.textContent = vietnameseStatus;
                        el.classList.remove('bg-blue-500/10', 'border-blue-500/20', 'text-blue-500',
                            'bg-tertiary-container/20', 'border-tertiary/20', 'text-tertiary',
                            'bg-error-container/20', 'border-error/20', 'text-error',
                            'bg-yellow-500/10', 'border-yellow-500/20', 'text-yellow-500');
                        if (j.status === 'Success') el.classList.add('bg-tertiary-container/20', 'border-tertiary/20', 'text-tertiary');
                        else if (j.status === 'Failed') el.classList.add('bg-error-container/20', 'border-error/20', 'text-error');
                        else if (j.status === 'Processing') el.classList.add('bg-blue-500/10', 'border-blue-500/20', 'text-blue-500');
                        else el.classList.add('bg-yellow-500/10', 'border-yellow-500/20', 'text-yellow-500');

                        // Manage dynamic spinner
                        let spinner = el.querySelector('.animate-spin');
                        if (j.status === 'Processing') {
                            if (!spinner) {
                                spinner = document.createElement('span');
                                spinner.className = 'w-3 h-3 border-2 border-t-transparent border-blue-500 rounded-full animate-spin';
                                el.insertBefore(spinner, txt);
                            }
                        } else {
                            if (spinner) {
                                spinner.remove();
                            }
                        }

                        if (j.status === 'Success' || j.status === 'Failed') {
                            setTimeout(() => location.reload(), 800);
                            return;
                        }
                    }
                } catch (e) { /* network blip, ignore */ }
            }
        }, 3000);
    }

    function escapeHtml(s) {
        return String(s).replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]));
    }
    function formatBytes(bytes) {
        if (!bytes) return '0 B';
        const units = ['B', 'KB', 'MB', 'GB'];
        let i = 0; let n = bytes;
        while (n >= 1024 && i < units.length - 1) { n /= 1024; i++; }
        return `${n.toFixed(1)} ${units[i]}`;
    }

    global.LmsUpload = {
        bindDropzone,
        uploadOneFile,
        startUploadQueue,
        pollDocumentStatus,
        formatBytes
    };
})(window);
