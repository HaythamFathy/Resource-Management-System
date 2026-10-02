var serviceDeskRoot = document.getElementById('serviceDeskRoot');
var serviceDeskIsApprover = !!serviceDeskRoot && serviceDeskRoot.dataset.isApprover === 'true';
var serviceDeskSelectionStorageKey = 'paperManagement.serviceDesk.selectedDepartmentIds.v1';
var serviceDeskLastDataList = [];
var serviceDeskComparisonChart = null;

var departmentLookupModalLastFocus = null;

function openDepartmentLookupModal() {
    var modal = document.getElementById('departmentLookupModal');
    if (!modal) return;

    departmentLookupModalLastFocus = document.activeElement;
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.classList.add('overflow-hidden');

    // Always start with the department picker itself closed.
    // The filter area stays visible; only the 'اختر جهة أو أكثر' dropdown opens on demand.
    closeServiceDeskPicker();

    // If a previous comparison chart exists, resize it after the modal becomes visible.
    if (serviceDeskComparisonChart && typeof serviceDeskComparisonChart.resize === 'function') {
        setTimeout(function () { serviceDeskComparisonChart.resize(); }, 60);
    }

    // Restore the user's last selection, but load only after the modal is visible so
    // charts and result widths are calculated correctly.
    if (getSelectedServiceDeskDepartmentIds().length > 0 && serviceDeskLastDataList.length === 0) {
        setTimeout(function () { loadDepartmentServiceDesk(); }, 0);
    }

    // Do not auto-focus any filter control when the lookup opens.
    // This keeps 'اختر جهة أو أكثر' genuinely closed until the user explicitly clicks it.
    setTimeout(closeServiceDeskPicker, 0);
    setTimeout(closeServiceDeskPicker, 120);
}

function closeDepartmentLookupModal() {
    var modal = document.getElementById('departmentLookupModal');
    if (!modal) return;

    modal.classList.add('hidden');
    modal.classList.remove('flex');
    closeServiceDeskPicker();

    var detailsModal = document.getElementById('serviceDeskRequestDetailsModal');
    var detailsOpen = detailsModal && !detailsModal.classList.contains('hidden');
    if (!detailsOpen) document.body.classList.remove('overflow-hidden');

    if (departmentLookupModalLastFocus && typeof departmentLookupModalLastFocus.focus === 'function') {
        departmentLookupModalLastFocus.focus();
    }
}

(function initDepartmentLookupModal() {
    var modal = document.getElementById('departmentLookupModal');
    var openBtn = document.getElementById('openDepartmentLookupModalBtn');
    var closeBtn = document.getElementById('closeDepartmentLookupModalBtn');
    if (!modal || !openBtn) return;

    openBtn.addEventListener('click', openDepartmentLookupModal);
    if (closeBtn) closeBtn.addEventListener('click', closeDepartmentLookupModal);

    var backdrop = modal.querySelector('[data-department-lookup-backdrop]');
    if (backdrop) backdrop.addEventListener('click', closeDepartmentLookupModal);

    document.addEventListener('keydown', function (e) {
        if (e.key !== 'Escape') return;

        var detailsModal = document.getElementById('serviceDeskRequestDetailsModal');
        if (detailsModal && !detailsModal.classList.contains('hidden')) {
            closeServiceDeskRequestDetails();
            return;
        }

        if (!modal.classList.contains('hidden')) closeDepartmentLookupModal();
    });
})();

(function () {
    var searchInput = document.getElementById('serviceDeskDeptSearch');
    if (!searchInput) return;

    searchInput.addEventListener('input', function () {
        filterServiceDeskDepartments(this.value);
        // Keep the department list closed. It opens only from the picker button.
    });

    searchInput.addEventListener('keydown', function (e) {
        if (e.key !== 'Enter') return;
        e.preventDefault();
        var visible = getVisibleServiceDeskCheckboxes();
        if (visible.length === 1) {
            visible[0].checked = true;
            updateServiceDeskSelection();
        }
        loadDepartmentServiceDesk();
    });

    document.addEventListener('click', function (e) {
        var card = document.getElementById('serviceDeskFilterCard');
        var panel = document.getElementById('serviceDeskDeptPickerPanel');
        if (!card || !panel || panel.classList.contains('hidden')) return;
        if (!card.contains(e.target)) closeServiceDeskPicker();
    });

    restoreServiceDeskSelection();
    updateServiceDeskSelection();

    // Keep the selection after refresh/navigation. When the lookup lives inside the
    // dashboard modal, defer loading until the modal is opened so chart sizing is correct.
    var lookupModal = document.getElementById('departmentLookupModal');
    if (getSelectedServiceDeskDepartmentIds().length > 0 && (!lookupModal || !lookupModal.classList.contains('hidden'))) {
        setTimeout(function () { loadDepartmentServiceDesk(); }, 0);
    }
})();

function serviceDeskEscapeHtml(value) {
    if (value === null || value === undefined) return '';
    return String(value)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#039;');
}

function serviceDeskNumber(value) {
    return typeof englishNumber === 'function' ? englishNumber(value) : String(value || 0);
}

function serviceDeskRezma(value) {
    return typeof rezmaText === 'function' ? rezmaText(value) : serviceDeskNumber(value) + ' رزم';
}

function serviceDeskUnit(value) {
    return typeof rezmaUnit === 'function' ? rezmaUnit(value) : (Number(value) === 1 ? 'رزمة' : 'رزم');
}


function serviceDeskRequestLink(requestId, text, extraClass) {
    var id = Number(requestId);
    if (!Number.isFinite(id) || id <= 0) return serviceDeskEscapeHtml(text || '');
    var label = text || ('طلب #' + serviceDeskNumber(id));
    return '<button type="button" onclick="openServiceDeskRequestDetails(' + id + ')" class="' +
        serviceDeskEscapeHtml(extraClass || 'font-black text-primary hover:underline underline-offset-2') +
        '" title="فتح تفاصيل الطلب">' + serviceDeskEscapeHtml(label) + '</button>';
}

function closeServiceDeskRequestDetails() {
    var modal = document.getElementById('serviceDeskRequestDetailsModal');
    if (!modal) return;
    modal.classList.add('hidden');
    modal.classList.remove('flex');

    // Keep background scrolling locked while the parent Department lookup modal is open.
    var lookupModal = document.getElementById('departmentLookupModal');
    if (!lookupModal || lookupModal.classList.contains('hidden')) {
        document.body.classList.remove('overflow-hidden');
    }
}

function serviceDeskDetailsNote(label, value) {
    if (!value) return '';
    return '<div class="rounded-xl border border-neutral-200 bg-white p-3">' +
        '<p class="text-[10px] font-black text-neutral-500 mb-1">' + serviceDeskEscapeHtml(label) + '</p>' +
        '<p class="text-xs font-bold text-neutral-800 whitespace-pre-wrap">' + serviceDeskEscapeHtml(value) + '</p>' +
        '</div>';
}

function serviceDeskDetailsTimeline(events) {
    if (!Array.isArray(events) || events.length === 0) {
        return '<p class="text-xs text-neutral-500 text-center py-4">لا يوجد سجل حركة للطلب حتى الآن.</p>';
    }
    return '<ol class="space-y-3">' + events.map(function (e) {
        var note = e.note ? '<p class="text-[10px] text-neutral-500 mt-1">' + serviceDeskEscapeHtml(e.note) + '</p>' : '';
        var qty = e.quantity ? '<span class="text-[10px] font-black text-[#34435E]"> · ' + serviceDeskRezma(e.quantity) + '</span>' : '';
        return '<li class="flex gap-3">' +
            '<span class="mt-1.5 w-2.5 h-2.5 rounded-full bg-[#58a4b0] shrink-0"></span>' +
            '<div class="min-w-0"><div class="flex flex-wrap items-center gap-1"><strong class="text-xs text-neutral-800">' + serviceDeskEscapeHtml(e.label || '') + '</strong>' + qty + '</div>' +
            '<p class="text-[10px] text-neutral-400 mt-0.5">' + serviceDeskEscapeHtml(e.createdDate || '') + (e.performedByName ? ' · ' + serviceDeskEscapeHtml(e.performedByName) : '') + '</p>' + note + '</div></li>';
    }).join('') + '</ol>';
}

function serviceDeskDetailsHistory(history) {
    var rows = history && Array.isArray(history.rows) ? history.rows : [];
    if (rows.length === 0) {
        return '<p class="text-xs text-neutral-500 text-center py-4">لا توجد طلبات سابقة لهذا النوع.</p>';
    }
    return '<div class="overflow-x-auto"><table class="w-full min-w-[650px] text-[11px]">' +
        '<thead class="bg-neutral-100 text-neutral-600"><tr><th class="p-2 text-right">رقم الطلب</th><th class="p-2 text-right">النوع</th><th class="p-2 text-right">الكمية</th><th class="p-2 text-right">الحالة</th><th class="p-2 text-right">التاريخ</th></tr></thead>' +
        '<tbody class="divide-y divide-neutral-100">' + rows.map(function (r) {
            var exception = r.isException ? '<span class="mr-1 text-[9px] font-black text-orange-700 bg-orange-50 border border-orange-200 rounded-full px-1.5 py-0.5">استثناء</span>' : '';
            return '<tr><td class="p-2 font-black text-primary">#' + serviceDeskNumber(r.requestID) + '</td>' +
                '<td class="p-2">' + serviceDeskEscapeHtml(r.paperTypeName || '') + exception + '</td>' +
                '<td class="p-2 font-black">' + serviceDeskRezma(r.requestedQuantity) + '</td>' +
                '<td class="p-2"><span class="' + serviceDeskEscapeHtml(r.statusClasses || '') + '">' + serviceDeskEscapeHtml(r.statusLabel || '') + '</span></td>' +
                '<td class="p-2 text-neutral-500">' + serviceDeskEscapeHtml(r.requestDate || '') + '</td></tr>';
        }).join('') + '</tbody></table></div>';
}

async function openServiceDeskRequestDetails(requestId) {
    var id = Number(requestId);
    if (!Number.isFinite(id) || id <= 0) return;

    var modal = document.getElementById('serviceDeskRequestDetailsModal');
    var body = document.getElementById('serviceDeskRequestDetailsBody');
    var title = document.getElementById('serviceDeskRequestDetailsTitle');
    if (!modal || !body || !title) return;

    title.textContent = 'طلب #' + serviceDeskNumber(id);
    body.innerHTML = '<div class="py-12 text-center text-neutral-500"><span class="material-symbols-outlined text-3xl animate-spin text-primary">progress_activity</span><p class="text-xs font-black mt-2">جارِ تحميل تفاصيل الطلب...</p></div>';
    modal.classList.remove('hidden');
    modal.classList.add('flex');
    document.body.classList.add('overflow-hidden');

    try {
        var detailResponse = await fetch('/PaperManagement/Request/DashboardRequestDetails?requestId=' + encodeURIComponent(id), {
            headers: { 'X-Requested-With': 'XMLHttpRequest' },
            cache: 'no-store',
            credentials: 'same-origin'
        });
        var detail = await detailResponse.json();
        if (!detailResponse.ok || !detail || detail.success === false) {
            throw new Error((detail && detail.message) || 'تعذر تحميل تفاصيل الطلب.');
        }

        var requests = await Promise.allSettled([
            fetch('/PaperManagement/Request/ActivityLog?requestId=' + encodeURIComponent(id), { cache: 'no-store', credentials: 'same-origin' }).then(function (r) { return r.ok ? r.json() : []; }),
            fetch('/PaperManagement/Request/DepartmentHistory?departmentId=' + encodeURIComponent(detail.departmentId) + '&paperTypeId=' + encodeURIComponent(detail.paperTypeId), { cache: 'no-store', credentials: 'same-origin' }).then(function (r) { return r.ok ? r.json() : { rows: [] }; })
        ]);
        var events = requests[0].status === 'fulfilled' ? requests[0].value : [];
        var history = requests[1].status === 'fulfilled' ? requests[1].value : { rows: [] };

        var exceptionBadge = detail.isException
            ? '<span class="inline-flex items-center rounded-full bg-orange-50 border border-orange-200 text-orange-700 px-2.5 py-1 text-[10px] font-black">استثناء</span>'
            : '';
        var reviewBadge = detail.needsReReview
            ? '<span class="inline-flex items-center gap-1 rounded-full bg-amber-100 border border-amber-300 text-amber-800 px-2.5 py-1 text-[10px] font-black"><span class="material-symbols-outlined text-xs">refresh</span>إعادة مراجعة</span>'
            : '';

        body.innerHTML = '<div class="space-y-4">' +
            '<div class="rounded-2xl bg-white border border-neutral-200 p-4">' +
            '<div class="flex flex-col lg:flex-row lg:items-start lg:justify-between gap-3">' +
            '<div><p class="text-[10px] font-black text-neutral-500">الجهة</p><h4 class="text-lg font-black text-[#34435E]">#' + serviceDeskNumber(detail.departmentId) + ' · ' + serviceDeskEscapeHtml(detail.departmentName || '') + '</h4>' +
            '<p class="text-xs text-neutral-500 mt-1">تاريخ الطلب: ' + serviceDeskEscapeHtml(detail.requestDate || '') + '</p></div>' +
            '<div class="flex flex-wrap items-center gap-1.5"><span class="' + serviceDeskEscapeHtml(detail.statusClasses || '') + '">' + serviceDeskEscapeHtml(detail.statusLabel || '') + '</span>' + reviewBadge + exceptionBadge + '</div>' +
            '</div>' +
            '<div class="grid grid-cols-1 sm:grid-cols-3 gap-3 mt-4">' +
            '<div class="rounded-xl bg-neutral-50 border border-neutral-200 p-3"><p class="text-[10px] text-neutral-500 font-bold">رقم الطلب</p><p class="text-xl font-black text-primary mt-1">#' + serviceDeskNumber(detail.requestId) + '</p></div>' +
            '<div class="rounded-xl bg-neutral-50 border border-neutral-200 p-3"><p class="text-[10px] text-neutral-500 font-bold">نوع الورق</p><p class="text-xl font-black text-neutral-800 mt-1">' + serviceDeskEscapeHtml(detail.paperTypeName || '') + '</p></div>' +
            '<div class="rounded-xl bg-neutral-50 border border-neutral-200 p-3"><p class="text-[10px] text-neutral-500 font-bold">الكمية المطلوبة</p><p class="text-xl font-black text-neutral-800 mt-1">' + serviceDeskRezma(detail.requestedQuantity) + '</p></div>' +
            '</div>' +
            '</div>' +

            '<div class="grid grid-cols-1 md:grid-cols-2 gap-3">' +
            serviceDeskDetailsNote('ملاحظات مقدم الطلب', detail.notes) +
            serviceDeskDetailsNote('ملاحظات أمين العهدة', detail.itReviewNotes) +
            serviceDeskDetailsNote('ملاحظات المعتمد الأول', detail.approverOneNote) +
            serviceDeskDetailsNote('ملاحظات المعتمد الثاني', detail.approverTwoNote) +
            serviceDeskDetailsNote('سبب الرفض', detail.rejectionNote) +
            serviceDeskDetailsNote('ملاحظات الصرف', detail.issueNotes) +
            '</div>' +

            '<div class="grid grid-cols-1 xl:grid-cols-2 gap-4">' +
            '<div class="rounded-2xl bg-white border border-neutral-200 overflow-hidden"><div class="px-4 py-3 border-b border-neutral-200"><h4 class="text-sm font-black text-on-surface">مسار الطلب</h4></div><div class="p-4">' + serviceDeskDetailsTimeline(events) + '</div></div>' +
            '<div class="rounded-2xl bg-white border border-neutral-200 overflow-hidden"><div class="px-4 py-3 border-b border-neutral-200 flex items-center justify-between"><div><h4 class="text-sm font-black text-on-surface">سجل طلبات الجهة لنفس النوع</h4><p class="text-[10px] text-neutral-500 mt-0.5">إجمالي المطلوب: ' + serviceDeskRezma(history.total || 0) + ' · تم صرف: ' + serviceDeskRezma(history.issued || 0) + '</p></div></div><div class="p-3">' + serviceDeskDetailsHistory(history) + '</div></div>' +
            '</div>' +
            '</div>';
    } catch (error) {
        body.innerHTML = '<div class="rounded-xl bg-red-50 border border-red-200 text-red-700 p-5 text-center"><span class="material-symbols-outlined text-3xl">error</span><p class="text-sm font-black mt-2">' + serviceDeskEscapeHtml(error && error.message ? error.message : 'تعذر تحميل تفاصيل الطلب.') + '</p></div>';
    }
}

document.addEventListener('keydown', function (e) {
    if (e.key === 'Escape') closeServiceDeskRequestDetails();
});

function serviceDeskDepartmentActionLinks(departmentId, compact) {
    var id = encodeURIComponent(departmentId);
    if (serviceDeskIsApprover) {
        if (compact) {
            return '<a onclick="persistServiceDeskSelection()" class="text-[9px] font-black text-primary hover:underline" href="/PaperManagement/Request/Pending?DepartmentID=' + id + '">طلبات اعتمادي</a>';
        }
        return '<a onclick="persistServiceDeskSelection()" href="/PaperManagement/Request/Pending?DepartmentID=' + id + '" class="inline-flex items-center gap-1.5 rounded-lg bg-[#34435E] text-white px-3 py-2 text-[11px] font-black hover:bg-[#2d394f]"><span class="material-symbols-outlined text-sm">fact_check</span>طلبات اعتمادي لهذه الجهة</a>';
    }

    if (compact) {
        return '<a onclick="persistServiceDeskSelection()" class="text-[9px] font-black text-primary hover:underline" href="/PaperManagement/Request/Reports?DepartmentID=' + id + '">التقرير</a>' +
            '<a onclick="persistServiceDeskSelection()" class="text-[9px] font-black text-[#2e6f7b] hover:underline" href="/PaperManagement/Request/ReviewQueue?DepartmentID=' + id + '">المراجعة</a>';
    }

    return '<a onclick="persistServiceDeskSelection()" href="/PaperManagement/Request/Reports?DepartmentID=' + id + '" class="inline-flex items-center gap-1.5 rounded-lg bg-white border border-neutral-200 hover:border-primary text-primary px-3 py-2 text-[11px] font-black"><span class="material-symbols-outlined text-sm">summarize</span>التقرير الكامل</a>' +
        '<a onclick="persistServiceDeskSelection()" href="/PaperManagement/Request/ReviewQueue?DepartmentID=' + id + '" class="inline-flex items-center gap-1.5 rounded-lg bg-[#34435E] text-white px-3 py-2 text-[11px] font-black hover:bg-[#2d394f]"><span class="material-symbols-outlined text-sm">fact_check</span>فتح المراجعة</a>';
}

function persistServiceDeskSelection() {
    try {
        var ids = getSelectedServiceDeskDepartmentIds();
        if (ids.length === 0) localStorage.removeItem(serviceDeskSelectionStorageKey);
        else localStorage.setItem(serviceDeskSelectionStorageKey, JSON.stringify(ids));
    } catch (e) {
        // Storage can be blocked by browser policy; the dashboard should still work normally.
    }
}

function restoreServiceDeskSelection() {
    try {
        var raw = localStorage.getItem(serviceDeskSelectionStorageKey);
        if (!raw) return;
        var ids = JSON.parse(raw);
        if (!Array.isArray(ids)) return;
        var wanted = ids.map(String);
        document.querySelectorAll('.service-desk-dept-chk').forEach(function (c) {
            c.checked = wanted.indexOf(String(c.value)) !== -1;
        });
    } catch (e) {
        // Ignore invalid/blocked storage.
    }
}

function getSelectedServiceDeskCheckboxes() {
    return Array.from(document.querySelectorAll('.service-desk-dept-chk:checked'));
}

function getSelectedServiceDeskDepartmentIds() {
    return getSelectedServiceDeskCheckboxes().map(function (c) { return c.value; });
}

function getVisibleServiceDeskCheckboxes() {
    return Array.from(document.querySelectorAll('.service-desk-dept-item'))
        .filter(function (item) { return item.style.display !== 'none'; })
        .map(function (item) { return item.querySelector('.service-desk-dept-chk'); })
        .filter(Boolean);
}

function toggleServiceDeskPicker(event) {
    if (event) event.stopPropagation();
    var panel = document.getElementById('serviceDeskDeptPickerPanel');
    if (!panel) return;
    if (panel.classList.contains('hidden')) openServiceDeskPicker();
    else closeServiceDeskPicker();
}

function openServiceDeskPicker() {
    var panel = document.getElementById('serviceDeskDeptPickerPanel');
    var btn = document.getElementById('serviceDeskDeptPickerBtn');
    var chevron = document.getElementById('serviceDeskPickerChevron');
    if (!panel) return;
    panel.classList.remove('hidden');
    if (btn) btn.setAttribute('aria-expanded', 'true');
    if (chevron) chevron.style.transform = 'rotate(180deg)';
}

function closeServiceDeskPicker() {
    var panel = document.getElementById('serviceDeskDeptPickerPanel');
    var btn = document.getElementById('serviceDeskDeptPickerBtn');
    var chevron = document.getElementById('serviceDeskPickerChevron');
    if (!panel) return;
    panel.classList.add('hidden');
    if (btn) btn.setAttribute('aria-expanded', 'false');
    if (chevron) chevron.style.transform = 'rotate(0deg)';
}

function filterServiceDeskDepartments(query) {
    var q = normalizeArabicSearch(query || '');
    document.querySelectorAll('.service-desk-dept-item').forEach(function (item) {
        var haystack = normalizeArabicSearch(item.getAttribute('data-search') || '');
        item.style.display = !q || haystack.indexOf(q) !== -1 ? '' : 'none';
    });
}

function selectVisibleServiceDeskDepartments() {
    getVisibleServiceDeskCheckboxes().forEach(function (c) { c.checked = true; });
    updateServiceDeskSelection();
}

function selectServiceDeskGroup(groupName) {
    var group = document.querySelector('[data-service-desk-group="' + groupName + '"]');
    if (!group) return;
    group.querySelectorAll('.service-desk-dept-chk').forEach(function (c) { c.checked = true; });
    updateServiceDeskSelection();
}

function clearServiceDeskDepartments() {
    document.querySelectorAll('.service-desk-dept-chk').forEach(function (c) { c.checked = false; });
    var search = document.getElementById('serviceDeskDeptSearch');
    if (search) search.value = '';
    filterServiceDeskDepartments('');
    serviceDeskLastDataList = [];
    destroyServiceDeskComparisonChart();
    updateServiceDeskSelection();

    var result = document.getElementById('serviceDeskResult');
    if (result) {
        result.innerHTML = '<div class="rounded-2xl border-2 border-dashed border-neutral-200 bg-neutral-50/70 px-5 py-8 text-center"><div class="w-12 h-12 mx-auto rounded-full bg-white border border-neutral-200 flex items-center justify-center text-neutral-400 mb-3"><span class="material-symbols-outlined text-2xl">touch_app</span></div><p class="font-extrabold text-neutral-700 text-sm">اختر جهة واحدة أو أكثر من فوق</p><p class="text-xs text-neutral-500 mt-1">تمت إعادة تعيين الاختيارات. لن تُحذف الاختيارات تلقائياً إلا من زر إعادة التعيين.</p></div>';
    }
}

function removeServiceDeskDepartment(id) {
    var checkbox = Array.from(document.querySelectorAll('.service-desk-dept-chk')).find(function (c) { return c.value === String(id); });
    if (checkbox) checkbox.checked = false;
    updateServiceDeskSelection();
}

function updateServiceDeskSelection() {
    var checked = getSelectedServiceDeskCheckboxes();
    var pickerText = document.getElementById('serviceDeskDeptPickerText');
    var count = document.getElementById('serviceDeskSelectedCount');
    var chips = document.getElementById('serviceDeskSelectedChips');
    var loadText = document.getElementById('serviceDeskLoadBtnText');

    if (pickerText) {
        if (checked.length === 0) pickerText.textContent = '— اختر جهة أو أكثر —';
        else if (checked.length === 1) pickerText.textContent = '#' + checked[0].value + ' — ' + (checked[0].dataset.name || '');
        else pickerText.textContent = 'تم تحديد ' + checked.length + ' جهات';
    }

    if (count) {
        count.textContent = checked.length;
        count.classList.toggle('hidden', checked.length === 0);
    }

    if (loadText) loadText.textContent = checked.length > 1 ? 'قارن الجهات' : 'اعرض المحدد';

    if (chips) {
        if (checked.length === 0) {
            chips.innerHTML = '';
            chips.classList.add('hidden');
        } else {
            chips.innerHTML = checked.map(function (c) {
                return '<span class="inline-flex items-center gap-1 rounded-full bg-white/10 border border-white/20 text-white px-2 py-1 text-[10px] font-black">#' +
                    serviceDeskEscapeHtml(c.value) + ' · ' + serviceDeskEscapeHtml(c.dataset.name || '') +
                    '<button type="button" onclick="removeServiceDeskDepartment(\'' + serviceDeskEscapeHtml(c.value) + '\')" class="w-4 h-4 rounded-full hover:bg-white/20 flex items-center justify-center" title="إزالة"><span class="material-symbols-outlined text-[12px]">close</span></button></span>';
            }).join('');
            chips.classList.remove('hidden');
        }
    }

    // Selection is intentionally sticky; only a user action (remove/reset) changes it.
    persistServiceDeskSelection();
}

function serviceDeskStatusCard(request, emptyText) {
    if (!request) {
        return '<div class="rounded-xl border border-neutral-200 bg-neutral-50 p-3 flex items-center justify-center text-center text-xs font-bold text-neutral-500">' + serviceDeskEscapeHtml(emptyText) + '</div>';
    }

    var reReviewBadge = request.needsReReview
        ? '<span class="inline-flex items-center gap-1 rounded-full bg-amber-100 border border-amber-300 text-amber-800 px-2 py-0.5 text-[10px] font-black"><span class="material-symbols-outlined text-xs">refresh</span>إعادة مراجعة</span>'
        : '';
    var exceptionBadge = request.isException
        ? '<span class="inline-flex items-center rounded-full bg-orange-50 border border-orange-200 text-orange-700 px-2 py-0.5 text-[10px] font-black">استثناء</span>'
        : '';

    return '' +
        '<div class="rounded-xl border border-neutral-200 bg-white p-3 shadow-xs">' +
        '<div class="flex items-start justify-between gap-3">' +
        '<div>' +
        '<p class="text-[10px] text-neutral-500 font-bold mb-1">' + serviceDeskRequestLink(request.requestId, 'طلب #' + serviceDeskNumber(request.requestId), 'font-black text-primary hover:underline underline-offset-2') + '</p>' +
        '<p class="text-sm font-black text-on-surface">' + serviceDeskEscapeHtml(request.paperTypeName) + ' · ' + serviceDeskRezma(request.quantity) + '</p>' +
        '</div>' +
        '<span class="' + serviceDeskEscapeHtml(request.statusClasses) + '">' + serviceDeskEscapeHtml(request.statusLabel) + '</span>' +
        '</div>' +
        '<div class="flex flex-wrap gap-1.5 mt-2">' + reReviewBadge + exceptionBadge + '</div>' +
        '<p class="text-[10px] text-neutral-500 mt-2">تاريخ الطلب: <strong class="text-neutral-700">' + serviceDeskEscapeHtml(request.requestDate || '') + '</strong></p>' +
        '</div>';
}

function renderServiceDeskQuotas(quotas) {
    if (!quotas || quotas.length === 0) {
        return '<div class="text-xs text-neutral-500 text-center py-4">لا توجد حصص ورق مسجلة لهذه الجهة.</div>';
    }

    return '<div class="grid grid-cols-1 md:grid-cols-2 gap-3">' + quotas.map(function (q) {
        var pct = Math.max(0, Math.min(100, Number(q.usagePercent) || 0));
        var indicator = pct >= 100 ? 'bg-red-500' : (pct >= 75 ? 'bg-amber-500' : 'bg-emerald-500');
        return '' +
            '<div class="rounded-xl border border-neutral-200 bg-neutral-50 p-4 min-h-[126px] flex flex-col justify-between">' +
            '<div class="flex items-center justify-between gap-2 mb-3">' +
            '<span class="font-black text-base text-on-surface">ورق ' + serviceDeskEscapeHtml(q.paperTypeName) + '</span>' +
            '<span class="text-[10px] font-black rounded-full bg-white border border-neutral-200 px-2.5 py-1">' + serviceDeskNumber(pct) + '% مستخدم</span>' +
            '</div>' +
            '<div class="grid grid-cols-3 gap-2 text-center mb-3">' +
            '<div class="bg-white rounded-lg border border-neutral-200 p-2.5"><p class="text-[9px] text-neutral-500">الحصة</p><p class="text-sm font-black text-primary mt-0.5">' + serviceDeskNumber(q.quotaLimit) + '</p></div>' +
            '<div class="bg-white rounded-lg border border-neutral-200 p-2.5"><p class="text-[9px] text-neutral-500">المستخدم</p><p class="text-sm font-black text-amber-700 mt-0.5">' + serviceDeskNumber(q.usedQuota) + '</p></div>' +
            '<div class="bg-white rounded-lg border border-neutral-200 p-2.5"><p class="text-[9px] text-neutral-500">المتبقي</p><p class="text-sm font-black text-emerald-700 mt-0.5">' + serviceDeskNumber(q.remainingQuota) + '</p></div>' +
            '</div>' +
            '<div class="h-2 rounded-full bg-neutral-200 overflow-hidden"><div class="h-full rounded-full ' + indicator + '" style="width:' + pct + '%"></div></div>' +
            '</div>';
    }).join('') + '</div>';
}

function renderServiceDeskRows(rows) {
    if (!rows || rows.length === 0) {
        return '<tr><td colspan="5" class="py-6 text-center text-xs text-neutral-500">لا توجد طلبات مسجلة لهذه الجهة.</td></tr>';
    }

    return rows.map(function (r) {
        var badges = '';
        if (r.needsReReview) badges += '<span class="text-[9px] font-black bg-amber-100 text-amber-800 border border-amber-300 rounded-full px-1.5 py-0.5">إعادة مراجعة</span>';
        if (r.isException) badges += '<span class="text-[9px] font-black bg-orange-50 text-orange-700 border border-orange-200 rounded-full px-1.5 py-0.5">استثناء</span>';
        return '' +
            '<tr class="hover:bg-primary/5 transition-colors">' +
            '<td class="py-2.5 px-3 font-black text-primary">' + serviceDeskRequestLink(r.requestId, '#' + serviceDeskNumber(r.requestId), 'font-black text-primary hover:underline underline-offset-2') + '</td>' +
            '<td class="py-2.5 px-3 font-semibold text-neutral-700">' + serviceDeskEscapeHtml(r.paperTypeName) + '</td>' +
            '<td class="py-2.5 px-3 font-black text-neutral-700">' + serviceDeskRezma(r.quantity) + '</td>' +
            '<td class="py-2.5 px-3 text-neutral-500">' + serviceDeskEscapeHtml(r.date) + '</td>' +
            '<td class="py-2.5 px-3"><div class="flex flex-wrap items-center gap-1"><span class="' + serviceDeskEscapeHtml(r.statusClasses) + '">' + serviceDeskEscapeHtml(r.statusLabel) + '</span>' + badges + '</div></td>' +
            '</tr>';
    }).join('');
}

function renderDepartmentServiceDesk(data) {
    var result = document.getElementById('serviceDeskResult');
    if (!result) return;

    var openAnswer = '';
    if (data.latestOpenRequest) {
        openAnswer = '<div class="rounded-xl bg-amber-50 border border-amber-200 px-4 py-3 flex items-start gap-3">' +
            '<span class="material-symbols-outlined text-amber-600 mt-0.5">location_searching</span>' +
            '<div><p class="text-sm font-extrabold text-amber-950">' + serviceDeskRequestLink(data.latestOpenRequest.requestId, 'الطلب #' + serviceDeskNumber(data.latestOpenRequest.requestId), 'font-black text-amber-950 hover:underline underline-offset-2') + ' موجود حالياً في مرحلة: <span class="underline">' + serviceDeskEscapeHtml(data.latestOpenRequest.statusLabel) + '</span>.</p></div>' +
            '</div>';
    } else if (data.latestRequest) {
        openAnswer = '<div class="rounded-xl bg-emerald-50 border border-emerald-200 px-4 py-3 flex items-start gap-3">' +
            '<span class="material-symbols-outlined text-emerald-600 mt-0.5">task_alt</span>' +
            '<div><p class="text-[10px] font-black text-emerald-700 mb-0.5">لا يوجد طلب مفتوح حالياً</p>' +
            '<p class="text-sm font-extrabold text-emerald-950">آخر طلب هو ' + serviceDeskRequestLink(data.latestRequest.requestId, '#' + serviceDeskNumber(data.latestRequest.requestId), 'font-black text-emerald-950 hover:underline underline-offset-2') + ' وحالته: ' + serviceDeskEscapeHtml(data.latestRequest.statusLabel) + '.</p></div>' +
            '</div>';
    } else {
        openAnswer = '<div class="rounded-xl bg-neutral-50 border border-neutral-200 px-4 py-3 text-sm font-bold text-neutral-600">هذه الجهة ليس لها طلبات مسجلة حتى الآن.</div>';
    }

    var lastIssued = data.lastIssuedRequest
        ? '<div class="rounded-xl border border-emerald-200 bg-emerald-50 p-3"><p class="text-[10px] font-black text-emerald-700">آخر صرف</p><p class="text-sm font-black text-emerald-950 mt-1">' + serviceDeskRequestLink(data.lastIssuedRequest.requestId, 'طلب #' + serviceDeskNumber(data.lastIssuedRequest.requestId), 'font-black text-emerald-950 hover:underline underline-offset-2') + ' · ' + serviceDeskRezma(data.lastIssuedRequest.quantity) + ' ' + serviceDeskEscapeHtml(data.lastIssuedRequest.paperTypeName) + '</p><p class="text-[10px] text-emerald-800 mt-1">' + serviceDeskEscapeHtml(data.lastIssuedRequest.issuedDate || data.lastIssuedRequest.requestDate || '') + '</p></div>'
        : '<div class="rounded-xl border border-neutral-200 bg-neutral-50 p-3"><p class="text-[10px] font-black text-neutral-500">آخر صرف</p><p class="text-sm font-black text-neutral-700 mt-1">لم يتم تسجيل صرف لهذه الجهة بعد.</p></div>';

    result.innerHTML = '' +
        '<div class="space-y-4">' +
        '<div class="rounded-2xl border border-neutral-200 bg-[#f8f9fa] p-4">' +
        '<div class="flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3">' +
        '<div class="flex items-center gap-3"><div class="w-10 h-10 rounded-xl bg-white border border-neutral-200 text-primary flex items-center justify-center font-black">#' + serviceDeskNumber(data.departmentId) + '</div><div><p class="text-[10px] text-neutral-500 font-bold">الجهة المختارة</p><h3 class="text-lg font-black text-on-surface">' + serviceDeskEscapeHtml(data.departmentName) + '</h3></div></div>' +
        '<div class="flex flex-wrap gap-2">' +
        serviceDeskDepartmentActionLinks(data.departmentId, false) +
        '</div></div></div>' +

        openAnswer +

        '<div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-2.5">' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">كل الطلبات</p><p class="text-xl font-black text-primary mt-1">' + serviceDeskNumber(data.lifetimeRequestCount) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">إجمالي المصروف</p><p class="text-xl font-black text-emerald-600 mt-1">' + serviceDeskNumber(data.lifetimeIssued) + '</p><span class="text-[9px] text-neutral-400">' + serviceDeskUnit(data.lifetimeIssued) + '</span></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">قيد المتابعة</p><p class="text-xl font-black text-amber-600 mt-1">' + serviceDeskNumber(data.pendingTotal) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">جاهز للصرف</p><p class="text-xl font-black text-blue-600 mt-1">' + serviceDeskNumber(data.readyToIssue) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">مرفوض</p><p class="text-xl font-black text-red-600 mt-1">' + serviceDeskNumber(data.rejectedCount) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">طلبات استثناء</p><p class="text-xl font-black text-orange-600 mt-1">' + serviceDeskNumber(data.exceptionCount) + '</p></div>' +
        '</div>' +

        '<div class="grid grid-cols-1 lg:grid-cols-[minmax(0,2fr)_minmax(270px,1fr)] gap-3 items-start">' +
        '<div class="rounded-2xl border border-neutral-200 bg-white p-4"><div class="flex items-center justify-between mb-3"><p class="text-xs font-black text-neutral-600">الحصة الشهرية حسب نوع الورق</p><span class="material-symbols-outlined text-primary">donut_large</span></div>' + renderServiceDeskQuotas(data.quotas) + '</div>' +
        '<div class="space-y-3 self-start">' + lastIssued + serviceDeskStatusCard(data.latestRequest, 'لا يوجد آخر طلب') + '</div>' +
        '</div>' +

        '<div class="rounded-2xl border border-neutral-200 bg-white overflow-hidden">' +
        '<div class="px-4 py-3 border-b border-neutral-200 flex items-center justify-between"><div><p class="text-sm font-black text-on-surface">آخر طلبات الجهة</p><p class="text-[10px] text-neutral-500">أحدث ' + serviceDeskNumber((data.requests || []).length) + ' من ' + serviceDeskNumber(data.lifetimeRequestCount) + ' طلب</p></div><span class="material-symbols-outlined text-neutral-400">history</span></div>' +
        '<div class="overflow-x-auto"><table class="w-full text-[11px]"><thead class="bg-neutral-100 text-neutral-600"><tr><th class="py-2.5 px-3 text-right">رقم الطلب</th><th class="py-2.5 px-3 text-right">النوع</th><th class="py-2.5 px-3 text-right">الكمية</th><th class="py-2.5 px-3 text-right">التاريخ</th><th class="py-2.5 px-3 text-right">الحالة</th></tr></thead><tbody class="divide-y divide-neutral-100">' + renderServiceDeskRows(data.requests) + '</tbody></table></div>' +
        '</div>' +
        '</div>';

    if (typeof normalizeVisibleNumbers === 'function') normalizeVisibleNumbers(result);
}

function serviceDeskSum(items, field) {
    return items.reduce(function (total, item) { return total + (Number(item[field]) || 0); }, 0);
}

function serviceDeskFindQuota(data, paperName) {
    var target = String(paperName || '').toUpperCase();
    return (data.quotas || []).find(function (q) {
        return String(q.paperTypeName || '').toUpperCase().indexOf(target) !== -1;
    }) || null;
}

function serviceDeskQuotaCompact(q) {
    if (!q) return '<span class="text-neutral-400">—</span>';
    return '<div class="min-w-[130px]"><div class="grid grid-cols-3 gap-1 text-center">' +
        '<div><span class="block text-[8px] text-neutral-400">الحصة</span><strong class="text-primary">' + serviceDeskNumber(q.quotaLimit) + '</strong></div>' +
        '<div><span class="block text-[8px] text-neutral-400">مستخدم</span><strong class="text-amber-700">' + serviceDeskNumber(q.usedQuota) + '</strong></div>' +
        '<div><span class="block text-[8px] text-neutral-400">متبقي</span><strong class="text-emerald-700">' + serviceDeskNumber(q.remainingQuota) + '</strong></div>' +
        '</div></div>';
}

function destroyServiceDeskComparisonChart() {
    if (serviceDeskComparisonChart) {
        try { serviceDeskComparisonChart.destroy(); } catch (e) { }
        serviceDeskComparisonChart = null;
    }
}

function serviceDeskQuotaPercent(q) {
    if (!q) return 0;
    var direct = Number(q.usagePercent);
    if (isFinite(direct)) return Math.max(0, Math.min(100, direct));
    var limit = Number(q.quotaLimit) || 0;
    var used = Number(q.usedQuota) || 0;
    return limit > 0 ? Math.max(0, Math.min(100, (used / limit) * 100)) : 0;
}

function renderServiceDeskComparisonChart(dataList, attempt) {
    destroyServiceDeskComparisonChart();
    var canvas = document.getElementById('serviceDeskComparisonChart');
    if (!canvas || !Array.isArray(dataList) || dataList.length === 0) return;

    if (typeof Chart === 'undefined') {
        var retry = Number(attempt) || 0;
        if (retry < 20) {
            setTimeout(function () {
                if (document.getElementById('serviceDeskComparisonChart')) renderServiceDeskComparisonChart(dataList, retry + 1);
            }, 100);
        }
        return;
    }

    var labels = dataList.map(function (d) { return '#' + serviceDeskNumber(d.departmentId) + ' · ' + d.departmentName; });
    var a4Usage = dataList.map(function (d) {
        var q = serviceDeskFindQuota(d, 'A4');
        return q ? Math.round(serviceDeskQuotaPercent(q) * 10) / 10 : null;
    });
    var a3Usage = dataList.map(function (d) {
        var q = serviceDeskFindQuota(d, 'A3');
        return q ? Math.round(serviceDeskQuotaPercent(q) * 10) / 10 : null;
    });

    var parent = canvas.parentElement;
    if (parent) parent.style.height = Math.max(260, dataList.length * 46) + 'px';

    serviceDeskComparisonChart = new Chart(canvas.getContext('2d'), {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [
                {
                    label: 'A4 مستخدم من الحصة',
                    data: a4Usage,
                    backgroundColor: 'rgba(52, 67, 94, 0.82)',
                    borderColor: 'rgba(52, 67, 94, 1)',
                    borderWidth: 1,
                    borderRadius: 6,
                    barPercentage: 0.72,
                    categoryPercentage: 0.72
                },
                {
                    label: 'A3 مستخدم من الحصة',
                    data: a3Usage,
                    backgroundColor: 'rgba(88, 164, 176, 0.82)',
                    borderColor: 'rgba(88, 164, 176, 1)',
                    borderWidth: 1,
                    borderRadius: 6,
                    barPercentage: 0.72,
                    categoryPercentage: 0.72
                }
            ]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            animation: { duration: 350 },
            interaction: { mode: 'index', intersect: false },
            plugins: {
                legend: {
                    position: 'top',
                    align: 'end',
                    labels: { usePointStyle: true, boxWidth: 8, font: { family: 'Cairo', size: 10, weight: 'bold' } }
                },
                tooltip: {
                    rtl: true,
                    callbacks: {
                        label: function (context) {
                            return context.dataset.label + ': ' + serviceDeskNumber(context.raw) + '%';
                        }
                    }
                }
            },
            scales: {
                x: {
                    beginAtZero: true,
                    min: 0,
                    max: 100,
                    grid: { color: 'rgba(0,0,0,0.05)' },
                    ticks: {
                        callback: function (value) { return serviceDeskNumber(value) + '%'; },
                        font: { family: 'Cairo', size: 9 }
                    }
                },
                y: {
                    grid: { display: false },
                    ticks: { autoSkip: false, font: { family: 'Cairo', size: 9, weight: 'bold' } }
                }
            }
        }
    });
}

function exportSelectedDepartmentsExcel() {
    var dataList = serviceDeskLastDataList || [];
    if (dataList.length === 0) {
        alert('اختر الجهات واضغط اعرض المحدد أولاً.');
        return;
    }

    persistServiceDeskSelection();

    var ids = dataList
        .map(function (d) { return Number(d.departmentId || 0); })
        .filter(function (id) { return id > 0; });

    if (ids.length === 0) {
        alert('تعذر تحديد الجهات المطلوبة للتصدير.');
        return;
    }

    var query = ids.map(function (id) {
        return 'departmentIds=' + encodeURIComponent(id);
    }).join('&');

    window.location.href = '/PaperManagement/Request/ExportSelectedDepartmentsLookupToExcel?' + query;
}

function printSelectedDepartmentsReport() {
    var dataList = serviceDeskLastDataList || [];
    if (dataList.length === 0) {
        alert('اختر الجهات واضغط اعرض المحدد أولاً.');
        return;
    }

    persistServiceDeskSelection();
    var popup = window.open('', '_blank', 'width=1050,height=850,scrollbars=yes');
    if (!popup) {
        alert('تعذر فتح نافذة الطباعة. اسمح بالنوافذ المنبثقة لهذا الموقع ثم حاول مرة أخرى.');
        return;
    }

    function lastIssuedDate(d) {
        if (!d || !d.lastIssuedRequest) return '—';
        var raw = d.lastIssuedRequest.issuedDate || d.lastIssuedRequest.requestDate || '';
        return raw ? serviceDeskEscapeHtml(String(raw).split(' ')[0]) : '—';
    }

    var rows = dataList.map(function (d) {
        var totalIssued = Number(d.lifetimeIssued || 0);
        var a4Issued = Number(d.lifetimeIssuedA4 || 0);
        var a3Issued = Number(d.lifetimeIssuedA3 || 0);
        return '<tr>' +
            '<td class="department-cell"><strong>' + serviceDeskEscapeHtml(d.departmentName) + '</strong></td>' +
            '<td class="issued-cell"><div class="issued-total">' + serviceDeskRezma(totalIssued) + '</div></td>' +
            '<td class="paper-cell paper-a4"><div class="paper-value">' + serviceDeskRezma(a4Issued) + '</div></td>' +
            '<td class="paper-cell paper-a3"><div class="paper-value">' + serviceDeskRezma(a3Issued) + '</div></td>' +
            '<td class="date-cell">' + lastIssuedDate(d) + '</td>' +
            '</tr>';
    }).join('');

    var generatedAt = new Date().toLocaleDateString('en-GB');
    var html = '<!doctype html><html dir="rtl" lang="ar"><head><meta charset="utf-8"><title>بيان المصروف للجهات المحددة</title>' +
        '<style>' +
        '@page{size:A4 portrait;margin:14mm}' +
        '*{box-sizing:border-box}' +
        'body{font-family:Tahoma,Arial,sans-serif;color:#243044;margin:0;background:#fff;font-size:11px;-webkit-print-color-adjust:exact;print-color-adjust:exact}' +
        '.no-print{text-align:left;margin-bottom:12px}' +
        '.printbtn{border:0;background:#34435E;color:#fff;border-radius:8px;padding:8px 17px;font-weight:700;cursor:pointer}' +
        '.report-head{display:flex;align-items:flex-end;justify-content:space-between;gap:16px;padding:4px 0 12px;margin-bottom:18px;border-bottom:2px solid #34435E}' +
        '.title-wrap{border-right:5px solid #34435E;padding-right:10px}' +
        '.title{font-size:22px;line-height:1.3;font-weight:800;color:#34435E;margin:0}' +
        '.report-date{color:#7b8492;font-size:10px;white-space:nowrap;padding-bottom:2px}' +
        '.table-wrap{border:1px solid #d9dee6;border-radius:12px;overflow:hidden}' +
        'table{width:100%;border-collapse:separate;border-spacing:0;table-layout:fixed}' +
        'th{background:#34435E;color:#fff;font-weight:700;padding:11px 12px;text-align:right;font-size:11px}' +
        'th:nth-child(1){width:38%}th:nth-child(2){width:20%;text-align:center}th:nth-child(3){width:13%;text-align:center;background:#3f5878}th:nth-child(4){width:13%;text-align:center;background:#327b82}th:nth-child(5){width:16%;text-align:center}' +
        'td{padding:12px;border-top:1px solid #e3e7ed;vertical-align:middle;background:#fff}' +
        'tbody tr:first-child td{border-top:0}' +
        'tbody tr:nth-child(even) td{background:#f8fafc}' +
        '.department-cell strong{font-size:11.5px;color:#2f3d55}' +
        '.issued-cell{text-align:center}' +
        '.issued-total{font-size:14px;font-weight:800;color:#34435E}' +
        '.paper-cell{text-align:center;font-variant-numeric:tabular-nums}' +
        '.paper-value{display:inline-block;min-width:62px;border-radius:9px;padding:6px 8px;font-size:11px;font-weight:800}' +
        '.paper-a4 .paper-value{background:#edf3fa;border:1px solid #d6e1ee;color:#3f5878}' +
        '.paper-a3 .paper-value{background:#ecf7f7;border:1px solid #d1e7e8;color:#327b82}' +
        '.date-cell{text-align:center;font-weight:700;color:#4b5563;font-variant-numeric:tabular-nums}' +
        '@media print{.no-print{display:none}.table-wrap{break-inside:auto}tr{break-inside:avoid;break-after:auto}}' +
        '</style></head><body>' +
        '<div class="no-print"><button class="printbtn" onclick="window.print()">طباعة التقرير</button></div>' +
        '<div class="report-head"><div class="title-wrap"><h1 class="title">بيان المصروف للجهات المحددة</h1></div><div class="report-date">تاريخ التقرير: ' + serviceDeskEscapeHtml(generatedAt) + '</div></div>' +
        '<div class="table-wrap"><table><thead><tr><th>الجهة</th><th>إجمالي المصروف</th><th>A4</th><th>A3</th><th>آخر صرف</th></tr></thead><tbody>' + rows + '</tbody></table></div>' +
        '</body></html>';

    popup.document.open();
    popup.document.write(html);
    popup.document.close();
    popup.focus();
    setTimeout(function () { try { popup.print(); } catch (e) { } }, 300);
}

function renderMultiDepartmentServiceDesk(dataList) {
    var result = document.getElementById('serviceDeskResult');
    if (!result) return;

    var totalRequests = serviceDeskSum(dataList, 'lifetimeRequestCount');
    var lifetimeIssued = serviceDeskSum(dataList, 'lifetimeIssued');
    var pendingTotal = serviceDeskSum(dataList, 'pendingTotal');
    var readyToIssue = serviceDeskSum(dataList, 'readyToIssue');
    var rejectedCount = serviceDeskSum(dataList, 'rejectedCount');
    var exceptionCount = serviceDeskSum(dataList, 'exceptionCount');
    var openCount = dataList.filter(function (d) { return !!d.latestOpenRequest; }).length;

    var comparisonRows = dataList.map(function (d) {
        var a4 = serviceDeskFindQuota(d, 'A4');
        var a3 = serviceDeskFindQuota(d, 'A3');
        var openRequest = d.latestOpenRequest;
        var locationCell;
        if (openRequest) {
            var locationBadges = '';
            if (openRequest.needsReReview) locationBadges += '<span class="inline-flex text-[8px] font-black bg-amber-100 text-amber-800 border border-amber-300 rounded-full px-1.5 py-0.5">إعادة مراجعة</span>';
            if (openRequest.isException) locationBadges += '<span class="inline-flex text-[8px] font-black bg-orange-50 text-orange-700 border border-orange-200 rounded-full px-1.5 py-0.5">استثناء</span>';
            locationCell = '<div class="space-y-1"><div>' + serviceDeskRequestLink(openRequest.requestId, 'طلب #' + serviceDeskNumber(openRequest.requestId), 'font-black text-primary hover:underline underline-offset-2') + '</div><div class="text-[10px] font-black text-amber-700">' + serviceDeskEscapeHtml(openRequest.statusLabel || 'قيد المتابعة') + '</div>' + (locationBadges ? '<div class="flex flex-wrap gap-1">' + locationBadges + '</div>' : '') + '</div>';
        } else {
            locationCell = '<span class="inline-flex items-center gap-1 text-[10px] font-black text-emerald-700"><span class="material-symbols-outlined text-sm">check_circle</span>لا يوجد طلب مفتوح</span>';
        }
        var lastIssued = d.lastIssuedRequest
            ? '<div><strong class="text-emerald-700">' + serviceDeskRezma(d.lastIssuedRequest.quantity) + '</strong><span class="text-neutral-500"> ' + serviceDeskEscapeHtml(d.lastIssuedRequest.paperTypeName) + '</span><span class="block text-[9px] text-neutral-400 mt-0.5">' + serviceDeskRequestLink(d.lastIssuedRequest.requestId, 'طلب #' + serviceDeskNumber(d.lastIssuedRequest.requestId) + ' · التفاصيل', 'font-black text-primary hover:underline underline-offset-2') + '</span></div>'
            : '<span class="text-neutral-400">لا يوجد</span>';
        return '<tr class="hover:bg-neutral-50 transition-colors">' +
            '<td class="py-3 px-3"><div class="font-black text-[#34435E]">#' + serviceDeskNumber(d.departmentId) + ' · ' + serviceDeskEscapeHtml(d.departmentName) + '</div><div class="flex gap-2 mt-1">' + serviceDeskDepartmentActionLinks(d.departmentId, true) + '</div></td>' +
            '<td class="py-3 px-3 font-black text-emerald-700">' + serviceDeskRezma(d.lifetimeIssued) + '</td>' +
            '<td class="py-3 px-3"><span class="font-black ' + (Number(d.pendingTotal) > 0 ? 'text-amber-700' : 'text-neutral-500') + '">' + serviceDeskNumber(d.pendingTotal) + '</span></td>' +
            '<td class="py-3 px-3 min-w-[150px]">' + locationCell + '</td>' +
            '<td class="py-3 px-3">' + lastIssued + '</td>' +
            '<td class="py-3 px-3">' + serviceDeskQuotaCompact(a4) + '</td>' +
            '<td class="py-3 px-3">' + serviceDeskQuotaCompact(a3) + '</td>' +
            '</tr>';
    }).join('');

    var mergedRequests = [];
    dataList.forEach(function (d) {
        (d.requests || []).forEach(function (r) {
            mergedRequests.push(Object.assign({}, r, { departmentId: d.departmentId, departmentName: d.departmentName }));
        });
    });
    mergedRequests.sort(function (a, b) { return Number(b.requestId) - Number(a.requestId); });
    mergedRequests = mergedRequests.slice(0, 10);

    var requestRows = mergedRequests.length ? mergedRequests.map(function (r) {
        var badges = '';
        if (r.needsReReview) badges += '<span class="text-[9px] font-black bg-amber-100 text-amber-800 border border-amber-300 rounded-full px-1.5 py-0.5">إعادة مراجعة</span>';
        if (r.isException) badges += '<span class="text-[9px] font-black bg-orange-50 text-orange-700 border border-orange-200 rounded-full px-1.5 py-0.5">استثناء</span>';
        return '<tr class="hover:bg-primary/5 transition-colors">' +
            '<td class="py-2.5 px-3 font-black text-primary">' + serviceDeskRequestLink(r.requestId, '#' + serviceDeskNumber(r.requestId), 'font-black text-primary hover:underline underline-offset-2') + '</td>' +
            '<td class="py-2.5 px-3 font-bold text-[#34435E]">#' + serviceDeskNumber(r.departmentId) + ' · ' + serviceDeskEscapeHtml(r.departmentName) + '</td>' +
            '<td class="py-2.5 px-3 font-semibold text-neutral-700">' + serviceDeskEscapeHtml(r.paperTypeName) + '</td>' +
            '<td class="py-2.5 px-3 font-black text-neutral-700">' + serviceDeskRezma(r.quantity) + '</td>' +
            '<td class="py-2.5 px-3 text-neutral-500">' + serviceDeskEscapeHtml(r.date) + '</td>' +
            '<td class="py-2.5 px-3"><div class="flex flex-wrap items-center gap-1"><span class="' + serviceDeskEscapeHtml(r.statusClasses) + '">' + serviceDeskEscapeHtml(r.statusLabel) + '</span>' + badges + '</div></td>' +
            '</tr>';
    }).join('') : '<tr><td colspan="6" class="py-6 text-center text-xs text-neutral-500">لا توجد طلبات للجهات المحددة.</td></tr>';

    var selectedBadges = dataList.map(function (d) {
        return '<span class="inline-flex items-center rounded-full bg-white border border-neutral-200 px-2 py-1 text-[10px] font-black text-[#34435E]">#' + serviceDeskNumber(d.departmentId) + ' · ' + serviceDeskEscapeHtml(d.departmentName) + '</span>';
    }).join('');

    result.innerHTML = '<div class="space-y-4">' +
        '<div class="rounded-2xl border border-neutral-200 bg-[#f8f9fa] p-4 flex flex-col lg:flex-row lg:items-center lg:justify-between gap-3"><div><p class="text-[10px] text-neutral-500 font-bold">مقارنة الجهات المختارة</p><h3 class="text-lg font-black text-on-surface">' + serviceDeskNumber(dataList.length) + ' جهات في شاشة واحدة</h3><div class="flex flex-wrap gap-1.5 mt-2">' + selectedBadges + '</div></div><div class="rounded-xl bg-white border border-neutral-200 px-4 py-2 text-center"><span class="block text-[9px] text-neutral-500 font-bold">طلبات مفتوحة الآن</span><strong class="text-xl font-black ' + (openCount > 0 ? 'text-amber-600' : 'text-emerald-600') + '">' + serviceDeskNumber(openCount) + '</strong></div></div>' +

        '<div class="grid grid-cols-2 md:grid-cols-3 xl:grid-cols-6 gap-2.5">' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">كل الطلبات</p><p class="text-xl font-black text-primary mt-1">' + serviceDeskNumber(totalRequests) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">إجمالي المصروف</p><p class="text-xl font-black text-emerald-600 mt-1">' + serviceDeskNumber(lifetimeIssued) + '</p><span class="text-[9px] text-neutral-400">' + serviceDeskUnit(lifetimeIssued) + '</span></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">قيد المتابعة</p><p class="text-xl font-black text-amber-600 mt-1">' + serviceDeskNumber(pendingTotal) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">جاهز للصرف</p><p class="text-xl font-black text-blue-600 mt-1">' + serviceDeskNumber(readyToIssue) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">مرفوض</p><p class="text-xl font-black text-red-600 mt-1">' + serviceDeskNumber(rejectedCount) + '</p></div>' +
        '<div class="rounded-xl bg-white border border-neutral-200 p-3"><p class="text-[9px] text-neutral-500 font-bold">طلبات استثناء</p><p class="text-xl font-black text-orange-600 mt-1">' + serviceDeskNumber(exceptionCount) + '</p></div>' +
        '</div>' +

        '<details class="group rounded-2xl border border-neutral-200 bg-white overflow-hidden"><summary class="list-none cursor-pointer select-none px-4 py-3 flex items-center justify-between gap-3 hover:bg-neutral-50 transition-colors"><div class="flex items-center gap-2"><span class="material-symbols-outlined text-primary">compare_arrows</span><p class="text-sm font-black text-on-surface">مقارنة المصروف والحصص</p></div><span class="material-symbols-outlined text-neutral-400 transition-transform group-open:rotate-180">expand_more</span></summary><div class="border-t border-neutral-200"><div class="px-4 py-3 flex flex-wrap justify-end gap-2"><button type="button" onclick="exportSelectedDepartmentsExcel(); event.stopPropagation();" class="inline-flex items-center gap-1.5 rounded-lg bg-emerald-600 hover:bg-emerald-700 text-white px-3 py-2 text-[10px] font-black shadow-sm"><span class="material-symbols-outlined text-sm">table_view</span>تصدير Excel</button><button type="button" onclick="printSelectedDepartmentsReport(); event.stopPropagation();" class="inline-flex items-center gap-1.5 rounded-lg bg-[#34435E] hover:bg-[#2d394f] text-white px-3 py-2 text-[10px] font-black shadow-sm"><span class="material-symbols-outlined text-sm">print</span>طباعة تقرير المحدد</button></div><div class="overflow-x-auto"><table class="w-full min-w-[1120px] text-[11px]"><thead class="bg-neutral-100 text-neutral-600"><tr><th class="py-2.5 px-3 text-right">الجهة</th><th class="py-2.5 px-3 text-right">إجمالي المصروف</th><th class="py-2.5 px-3 text-right">قيد المتابعة</th><th class="py-2.5 px-3 text-right">مكان الطلب الآن</th><th class="py-2.5 px-3 text-right">آخر صرف</th><th class="py-2.5 px-3 text-right">A4</th><th class="py-2.5 px-3 text-right">A3</th></tr></thead><tbody class="divide-y divide-neutral-100">' + comparisonRows + '</tbody></table></div></div></details>' +

        '<details class="group rounded-2xl border border-neutral-200 bg-white overflow-hidden"><summary class="list-none cursor-pointer select-none px-4 py-3 flex items-center justify-between gap-3 hover:bg-neutral-50 transition-colors"><div class="flex items-center gap-2"><span class="material-symbols-outlined text-neutral-500">history</span><p class="text-sm font-black text-on-surface">أحدث الطلبات للجهات المحددة</p><span class="text-[9px] font-bold text-neutral-400">آخر 10 فقط</span><span class="rounded-full bg-neutral-100 border border-neutral-200 text-neutral-600 px-2 py-0.5 text-[9px] font-black">' + serviceDeskNumber(mergedRequests.length) + '</span></div><span class="material-symbols-outlined text-neutral-400 transition-transform group-open:rotate-180">expand_more</span></summary><div class="border-t border-neutral-200 overflow-x-auto"><table class="w-full min-w-[820px] text-[11px]"><thead class="bg-neutral-100 text-neutral-600"><tr><th class="py-2.5 px-3 text-right">رقم الطلب</th><th class="py-2.5 px-3 text-right">الجهة</th><th class="py-2.5 px-3 text-right">النوع</th><th class="py-2.5 px-3 text-right">الكمية</th><th class="py-2.5 px-3 text-right">التاريخ</th><th class="py-2.5 px-3 text-right">الحالة</th></tr></thead><tbody class="divide-y divide-neutral-100">' + requestRows + '</tbody></table></div></details>' +
        '</div>';

    if (typeof normalizeVisibleNumbers === 'function') normalizeVisibleNumbers(result);
}

function fetchServiceDeskDepartment(departmentId) {
    return fetch('/PaperManagement/Request/GetDepartmentDashboardDetail?departmentId=' + encodeURIComponent(departmentId), {
        headers: { 'X-Requested-With': 'XMLHttpRequest' }
    }).then(function (res) {
        if (!res.ok) throw new Error('تعذر تحميل بيانات الجهة #' + departmentId);
        return res.json();
    }).then(function (data) {
        if (!data.success) throw new Error(data.message || 'تعذر تحميل بيانات الجهة #' + departmentId);
        return data;
    });
}

function fetchServiceDeskDepartments(ids, concurrency) {
    var results = new Array(ids.length);
    var nextIndex = 0;
    var workerCount = Math.min(concurrency || 4, ids.length);

    function worker() {
        var index = nextIndex++;
        if (index >= ids.length) return Promise.resolve();
        return fetchServiceDeskDepartment(ids[index]).then(function (data) {
            results[index] = data;
            return worker();
        });
    }

    var workers = [];
    for (var i = 0; i < workerCount; i++) workers.push(worker());
    return Promise.all(workers).then(function () { return results; });
}

function loadDepartmentServiceDesk() {
    var result = document.getElementById('serviceDeskResult');
    var btn = document.getElementById('serviceDeskLoadBtn');
    if (!result) return;

    var departmentIds = getSelectedServiceDeskDepartmentIds();
    if (departmentIds.length === 0) {
        result.innerHTML = '<div class="rounded-xl bg-amber-50 border border-amber-200 text-amber-800 px-4 py-3 text-xs font-black text-center">اختر جهة واحدة على الأقل أولاً.</div>';
        openServiceDeskPicker();
        return;
    }

    closeServiceDeskPicker();
    if (btn) {
        btn.disabled = true;
        btn.classList.add('opacity-70', 'cursor-wait');
    }

    var loadingLabel = departmentIds.length === 1
        ? 'جاري جمع بيانات الجهة...'
        : 'جاري جمع ومقارنة بيانات ' + serviceDeskNumber(departmentIds.length) + ' جهات...';
    result.innerHTML = '<div class="rounded-2xl border border-neutral-200 bg-neutral-50 p-8 text-center"><span class="material-symbols-outlined text-primary text-3xl animate-spin">progress_activity</span><p class="text-xs font-black text-neutral-600 mt-2">' + loadingLabel + '</p></div>';

    fetchServiceDeskDepartments(departmentIds, 4)
        .then(function (dataList) {
            serviceDeskLastDataList = dataList.slice();
            persistServiceDeskSelection();
            if (dataList.length === 1) {
                destroyServiceDeskComparisonChart();
                renderDepartmentServiceDesk(dataList[0]);
            } else {
                renderMultiDepartmentServiceDesk(dataList);
            }
        })
        .catch(function (err) {
            result.innerHTML = '<div class="rounded-xl bg-red-50 border border-red-200 text-red-700 px-4 py-4 text-xs font-black text-center">' + serviceDeskEscapeHtml(err.message) + '</div>';
        })
        .finally(function () {
            if (btn) {
                btn.disabled = false;
                btn.classList.remove('opacity-70', 'cursor-wait');
            }
        });
}
