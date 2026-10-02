// Shared UI helpers used by all paper-management pages.

// Keep every system-rendered number in Latin/English digits, regardless of
// the browser or operating-system Arabic locale.
function englishNumber(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return String(value ?? '');
    return number.toLocaleString('en-US');
}

// Paper-bundle grammar: exactly 1 => "رزمة"; every other quantity => "رزم".
function rezmaUnit(quantity) {
    return Number(quantity) === 1 ? 'رزمة' : 'رزم';
}

function rezmaText(quantity) {
    return englishNumber(quantity) + ' ' + rezmaUnit(quantity);
}

// Defensive display normalizer. Server rendering already uses en-US culture,
// but this also catches Arabic-Indic digits that may be injected by dynamic UI
// code or returned from older database content.
function normalizeEnglishDigits(text) {
    if (text === null || text === undefined) return text;
    return String(text)
        .replace(/[٠-٩]/g, function (d) { return String('٠١٢٣٤٥٦٧٨٩'.indexOf(d)); })
        .replace(/[۰-۹]/g, function (d) { return String('۰۱۲۳۴۵۶۷۸۹'.indexOf(d)); });
}

// Search-only Arabic normalizer. It never changes stored or displayed names;
// it only makes user searches tolerant of common Arabic spelling/typing variants.
function normalizeArabicSearch(text) {
    if (text === null || text === undefined) return '';

    return normalizeEnglishDigits(String(text))
        .toLowerCase()
        .replace(/[أإآٱ]/g, 'ا')
        .replace(/ى/g, 'ي')
        .replace(/ؤ/g, 'و')
        .replace(/ئ/g, 'ي')
        .replace(/ة/g, 'ه')
        .replace(/ک/g, 'ك')
        .replace(/ی/g, 'ي')
        .replace(/ـ/g, '')
        .replace(/[\u064B-\u065F\u0670\u06D6-\u06ED]/g, '')
        .replace(/\s+/g, ' ')
        .trim();
}
function normalizeVisibleNumbers(root) {
    const target = root || document.body;
    if (!target) return;

    const walker = document.createTreeWalker(target, NodeFilter.SHOW_TEXT);
    const nodes = [];
    let node;
    while ((node = walker.nextNode())) nodes.push(node);

    nodes.forEach(function (textNode) {
        const normalized = normalizeEnglishDigits(textNode.nodeValue);
        if (normalized !== textNode.nodeValue) textNode.nodeValue = normalized;
    });
}

document.addEventListener('DOMContentLoaded', function () {
    normalizeVisibleNumbers(document.body);
});
