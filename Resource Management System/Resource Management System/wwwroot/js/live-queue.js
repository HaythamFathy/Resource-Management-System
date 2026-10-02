(function (window, document) {
    'use strict';

    let eventSource = null;
    let syncInProgress = false;
    let syncQueued = false;
    let activeOptions = null;

    function escapeHtml(value) {
        return String(value ?? '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#039;');
    }

    function formatNumber(value) {
        const number = Number(value);
        if (!Number.isFinite(number)) return '0';
        return number.toLocaleString('en-US');
    }

    function unitFor(value) {
        if (typeof window.rezmaUnit === 'function') {
            return window.rezmaUnit(value);
        }
        return Number(value) === 1 ? 'رزمة' : 'رزم';
    }

    function quantityText(value) {
        return formatNumber(value) + ' ' + unitFor(value);
    }

    function setText(id, value) {
        const element = document.getElementById(id);
        if (element) element.textContent = value;
    }

    function findHistoryBody(requestId) {
        return document.getElementById('history-' + requestId)
            || document.getElementById('history-rows-' + requestId);
    }

    function historyRowHtml(row) {
        const exceptionBadge = row.isException
            ? ' <span class="inline-flex items-center gap-1 text-[10px] font-bold text-amber-800 bg-amber-100 border border-amber-300 px-2 py-0.5 rounded-full whitespace-nowrap"><i class="bi bi-exclamation-triangle-fill"></i>استثناء</span>'
            : '';

        const statusClasses = typeof row.statusClasses === 'string' && row.statusClasses.trim()
            ? row.statusClasses
            : 'status-badge status-default';

        return '<tr class="border-b last:border-b-0">'
            + '<td class="p-3 font-bold text-primary">#' + formatNumber(row.requestID) + '</td>'
            + '<td class="p-3"><div class="flex items-center gap-1.5 flex-wrap">'
            + escapeHtml(row.paperTypeName) + exceptionBadge + '</div></td>'
            + '<td class="p-3 whitespace-nowrap">' + quantityText(row.requestedQuantity) + '</td>'
            + '<td class="p-3"><span class="' + escapeHtml(statusClasses) + '">' + escapeHtml(row.statusLabel) + '</span></td>'
            + '<td class="p-3 text-gray-500 whitespace-nowrap">' + escapeHtml(row.requestDate) + '</td>'
            + '</tr>';
    }

    async function fetchJson(url, timeoutMs) {
        const controller = new AbortController();
        const timer = window.setTimeout(function () { controller.abort(); }, timeoutMs || 12000);
        try {
            const response = await fetch(url, {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                cache: 'no-store',
                credentials: 'same-origin',
                signal: controller.signal
            });
            if (!response.ok) throw new Error('HTTP ' + response.status);
            return await response.json();
        } finally {
            window.clearTimeout(timer);
        }
    }

    async function loadRequestDetails(requestId) {
        const modal = document.getElementById('request-' + requestId);
        if (!modal) return;

        const departmentId = Number(modal.dataset.departmentId || 0);
        const paperTypeId = Number(modal.dataset.papertypeId || 0);

        // The Issue modal only needs its activity log and does not carry
        // department/paper-type data attributes. In that case there is
        // intentionally nothing for this helper to load.
        if (!departmentId) return;

        const historyBody = findHistoryBody(requestId);
        if (historyBody) {
            historyBody.innerHTML = '<tr><td colspan="5" class="p-3 text-center text-gray-400">جارِ التحميل...</td></tr>';
        }

        try {
            const query = new URLSearchParams({ departmentId: String(departmentId) });
            if (paperTypeId > 0) query.set('paperTypeId', String(paperTypeId));

            const data = await fetchJson('/PaperManagement/Request/DepartmentHistory?' + query.toString(), 12000);

            setText('total-' + requestId, quantityText(data.total || 0));
            setText('issued-' + requestId, quantityText(data.issued || 0));

            if (!historyBody) return;

            const rows = Array.isArray(data.rows) ? data.rows : [];
            if (!rows.length) {
                historyBody.innerHTML = '<tr><td colspan="5" class="p-4 text-center text-gray-400">لا توجد طلبات سابقة.</td></tr>';
                return;
            }

            historyBody.innerHTML = rows.map(historyRowHtml).join('');
        } catch (error) {
            const message = error && error.name === 'AbortError'
                ? 'انتهت مهلة تحميل البيانات. أغلق التفاصيل وافتحها مرة أخرى.'
                : 'تعذر تحميل بيانات الطلب.';

            setText('total-' + requestId, 'تعذر التحميل');
            setText('issued-' + requestId, 'تعذر التحميل');

            if (historyBody) {
                historyBody.innerHTML = '<tr><td colspan="5" class="p-4 text-center text-red-600">'
                    + escapeHtml(message) + '</td></tr>';
            }
        }
    }

    async function syncQueue() {
        if (syncInProgress) {
            syncQueued = true;
            return;
        }

        syncInProgress = true;
        const scrollY = window.scrollY;

        try {
            const response = await fetch(window.location.href, {
                method: 'GET',
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                cache: 'no-store',
                credentials: 'same-origin'
            });
            if (!response.ok) throw new Error('HTTP ' + response.status);

            const html = await response.text();
            const parsed = new DOMParser().parseFromString(html, 'text/html');

            const currentContent = document.getElementById('liveQueueContent');
            const incomingContent = parsed.getElementById('liveQueueContent');
            if (currentContent && incomingContent) {
                currentContent.innerHTML = incomingContent.innerHTML;
            }

            const currentModals = document.getElementById('liveQueueModals');
            const incomingModals = parsed.getElementById('liveQueueModals');
            if (currentModals && incomingModals) {
                currentModals.innerHTML = incomingModals.innerHTML;
            }

            if (activeOptions && typeof activeOptions.onSynced === 'function') {
                activeOptions.onSynced();
            }

            // Replacing a table while the user is lower on the page used to
            // produce an apparent jump/flicker around arrival badges.
            window.scrollTo({ top: scrollY, left: window.scrollX, behavior: 'auto' });
        } catch (error) {
            // SSE will reconnect by itself. A transient refresh failure should
            // not destroy the current queue or leave the page unusable.
            console.error('Live queue refresh failed:', error);
        } finally {
            syncInProgress = false;
            if (syncQueued) {
                syncQueued = false;
                void syncQueue();
            }
        }
    }

    function start(options) {
        activeOptions = options || {};
        if (!activeOptions.eventsUrl || typeof window.EventSource === 'undefined') return;

        if (eventSource) {
            eventSource.close();
            eventSource = null;
        }

        eventSource = new EventSource(activeOptions.eventsUrl, { withCredentials: true });
        eventSource.addEventListener('queueChanged', function () {
            void syncQueue();
        });
        eventSource.onerror = function () {
            // EventSource performs its own retry. Do not reload the page here.
        };
    }

    function stop() {
        if (eventSource) {
            eventSource.close();
            eventSource = null;
        }
    }

    window.LiveQueue = {
        start: start,
        stop: stop,
        sync: syncQueue,
        loadRequestDetails: loadRequestDetails
    };
})(window, document);
