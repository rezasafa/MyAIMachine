'use strict';
const $ = id => document.getElementById(id);
const csrf = document.querySelector('input[name="__RequestVerificationToken"]').value;
let conversationId = null, busy = false, navigation = 0;
async function api(path, method = 'GET', body, admin = false) {
    const headers = { 'X-CSRF-TOKEN': csrf };
    if (body !== undefined) headers['Content-Type'] = 'application/json';
    if (admin) headers['X-Admin-Key'] = $('admin-key').value;
    const response = await fetch(path, { method, headers, body: body === undefined ? undefined : JSON.stringify(body) });
    if (!response.ok) {
        const data = await response.json().catch(() => ({}));
        throw new Error(data.detail || (data.errors ? Object.values(data.errors).flat().join(' ') : '') || (response.status === 429 ? 'تعداد درخواست‌ها زیاد است؛ کمی صبر کنید.' : 'درخواست انجام نشد. دوباره تلاش کنید.'));
    }
    return response.status === 204 ? null : response.json();
}
function showMessage(role, text, similarity) {
    $('welcome').hidden = true;
    const node = document.createElement('div');
    node.className = 'message ' + (role === 'user' ? 'user' : 'assistant');
    node.textContent = text; // Never interpret chat/training content as HTML.
    if (role === 'assistant' && similarity != null) {
        const meta = document.createElement('small');
        meta.textContent = 'شباهت با نمونه‌های آموزشی: ' + Math.round(similarity * 100).toLocaleString('fa-IR') + '٪';
        node.append(meta);
    }
    $('messages').append(node);
    $('chat-view').scrollTop = $('chat-view').scrollHeight;
    return node;
}
async function history() {
    const rows = await api('/api/chat');
    $('history').replaceChildren();
    if (!rows.length) {
        const empty = document.createElement('p'); empty.className = 'empty-history';
        empty.textContent = 'اولین گفتگوی شما از اینجا آغاز می‌شود.'; $('history').append(empty);
    }
    for (const row of rows) {
        const item = document.createElement('div'); item.className = 'history-row' + (row.id === conversationId ? ' active' : '');
        const button = document.createElement('button'); button.textContent = row.title; button.title = row.title;
        button.onclick = () => openChat(row.id);
        const remove = document.createElement('button'); remove.className = 'delete'; remove.textContent = '×'; remove.setAttribute('aria-label', 'حذف ' + row.title);
        remove.onclick = async () => {
            if (busy || !confirm('این گفتگو حذف شود؟')) return;
            try { await api('/api/chat/' + row.id, 'DELETE'); if (conversationId === row.id) reset(); await history(); }
            catch (error) { $('status').textContent = error.message; }
        };
        item.append(button, remove); $('history').append(item);
    }
}
function reset() {
    if (busy) return;
    navigation++; conversationId = null; $('messages').replaceChildren(); $('welcome').hidden = false; $('status').textContent = '';
    document.querySelectorAll('.history-row').forEach(x => x.classList.remove('active'));
    document.body.classList.remove('sidebar-open'); $('message').focus();
}
async function openChat(id) {
    if (busy) return;
    const ticket = ++navigation;
    try {
        const rows = await api('/api/chat/' + id);
        if (ticket !== navigation || busy) return;
        conversationId = id; $('messages').replaceChildren(); $('welcome').hidden = rows.length > 0;
        rows.forEach(x => showMessage(x.role, x.content, x.similarity));
        document.body.classList.remove('sidebar-open'); await history();
    } catch (error) { $('status').textContent = error.message; }
}
$('chat-form').addEventListener('submit', async event => {
    event.preventDefault(); const message = $('message').value.trim(); if (!message || busy) return;
    busy = true; navigation++; $('send').disabled = true; $('message').disabled = true;
    const pending = showMessage('user', message); $('message').value = ''; $('message').style.height = 'auto';
    $('status').textContent = 'در حال بررسی نمونه‌های آموخته‌شده…';
    try {
        const reply = await api('/api/chat', 'POST', { conversationId, message });
        conversationId = reply.conversationId; showMessage('assistant', reply.text, reply.similarity); $('status').textContent = '';
        try { await history(); } catch { $('status').textContent = 'پاسخ ذخیره شد؛ بارگذاری تاریخچه ناموفق بود.'; }
    } catch (error) { pending.remove(); $('message').value = message; $('status').textContent = error.message; }
    finally { busy = false; $('send').disabled = false; $('message').disabled = false; $('message').focus(); }
});
$('message').addEventListener('keydown', event => { if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) { event.preventDefault(); $('chat-form').requestSubmit(); } });
$('message').addEventListener('input', () => { $('message').style.height = 'auto'; $('message').style.height = Math.min($('message').scrollHeight, 160) + 'px'; });
$('new-chat').onclick = reset;
$('toggle-sidebar').onclick = () => document.body.classList.toggle('sidebar-open');
$('chat-view').onclick = () => document.body.classList.remove('sidebar-open');
document.querySelectorAll('[data-prompt]').forEach(button => { button.onclick = () => { $('message').value = button.dataset.prompt; $('message').focus(); }; });
let editingId = null, trainingPage = 1, trainingPages = 1, adminBusy = false;
function openTraining() { $('training-dialog').showModal(); document.body.classList.remove('sidebar-open'); }
$('open-training').onclick = openTraining; $('teach-start').onclick = openTraining;
$('close-training').onclick = () => { if (!adminBusy) $('training-dialog').close(); };
$('training-dialog').addEventListener('cancel', event => { if (adminBusy) event.preventDefault(); });
$('training-dialog').addEventListener('close', () => { $('admin-key').value = ''; $('training-content').hidden = true; $('training-status').textContent = ''; cancelEdit(); $('preview-result').hidden = true; });
async function adminAction(action, message) {
    if (adminBusy) return;
    adminBusy = true;
    const buttons = [...$('training-dialog').querySelectorAll('button, input, textarea')]; buttons.forEach(b => b.disabled = true);
    $('training-status').textContent = 'در حال انجام…';
    try { await action(); $('training-status').textContent = message; }
    catch (error) { $('training-status').textContent = error.message; }
    finally { adminBusy = false; buttons.forEach(b => b.disabled = false); updatePagination(); }
}
async function loadTraining() {
    const query = new URLSearchParams({ page: trainingPage, search: $('example-search').value });
    const data = await api('/api/training?' + query, 'GET', undefined, true);
    trainingPage = data.page; trainingPages = data.pages;
    $('training-content').hidden = false;
    $('model-summary').textContent = `${data.total} نمونه ذخیره‌شده · ` + (data.version ? `نسخه ${data.version.id} با ${data.version.exampleCount} نمونه آموزش دیده است.` : 'مدل هنوز آموزش ندیده است.');
    $('model-summary').textContent += data.needsTraining ? ' تغییرات هنوز آموزش داده نشده‌اند.' : ' داده‌های آموزشی با مدل فعال هماهنگ‌اند.';
    $('page-summary').textContent = `صفحه ${data.page} از ${data.pages} · ${data.filteredTotal} نتیجه`;
    updatePagination();
    $('threshold').value = data.settings.minimumSimilarity; $('fallback').value = data.settings.fallbackResponse;
    $('examples').replaceChildren();
    if (!data.examples.length) $('examples').textContent = 'نمونه‌ای یافت نشد.';
    for (const example of data.examples) {
        const row = document.createElement('div'); row.className = 'example';
        const text = document.createElement('div'); text.textContent = example.input;
        const response = document.createElement('small'); response.textContent = example.response; text.append(response);
        const remove = document.createElement('button'); remove.textContent = 'حذف';
        remove.onclick = () => { if (adminBusy || !confirm('این نمونه حذف شود؟ پس از حذف، مدل را بازآموزی کنید.')) return; adminAction(async () => { await api('/api/training/examples/' + example.id, 'DELETE', undefined, true); if (editingId === example.id) cancelEdit(); await loadTraining(); }, 'نمونه حذف شد؛ برای اعمال تغییر مدل را بازآموزی کنید.'); };
        const edit = document.createElement('button'); edit.textContent = 'ویرایش';
        edit.onclick = () => {
            if (adminBusy) return;
            editingId = example.id; $('teach-input').value = example.input; $('teach-response').value = example.response;
            $('save-example').textContent = 'ذخیره ویرایش'; $('cancel-edit').hidden = false; $('teach-input').focus();
        };
        row.append(text, edit, remove); $('examples').append(row);
    }
}
$('load-training').onclick = () => adminAction(loadTraining, 'اطلاعات مدل بارگذاری شد.');
$('teach-form').onsubmit = event => { event.preventDefault(); adminAction(async () => {
    await api('/api/training/examples' + (editingId === null ? '' : '/' + editingId), editingId === null ? 'POST' : 'PUT', { input: $('teach-input').value, response: $('teach-response').value }, true);
    cancelEdit(); await loadTraining();
}, 'نمونه ذخیره شد. برای یادگیری، بازآموزی مدل را بزنید.'); };
$('train-model').onclick = () => adminAction(async () => { await api('/api/training/train', 'POST', undefined, true); await loadTraining(); }, 'آموزش پایان یافت. مدل جدید آماده گفتگو است.');
$('settings-form').onsubmit = event => { event.preventDefault(); adminAction(async () => {
    await api('/api/training/settings', 'PUT', { minimumSimilarity: Number($('threshold').value), fallbackResponse: $('fallback').value }, true);
}, 'تنظیمات ذخیره شد.'); };
$('load-logs').onclick = () => adminAction(async () => {
    const logs = await api('/api/training/logs', 'GET', undefined, true);
    $('logs').textContent = logs.map(x => `${x.createdAt} | ${x.level} | ${x.event} | ${x.detail}`).join('\n');
}, 'لاگ‌ها بارگذاری شدند.');
history().catch(error => { $('status').textContent = error.message; });

function updatePagination() {
    $('previous-page').disabled = adminBusy || trainingPage <= 1;
    $('next-page').disabled = adminBusy || trainingPage >= trainingPages;
}
function cancelEdit() {
    editingId = null; $('teach-form').reset(); $('save-example').textContent = 'ذخیره نمونه'; $('cancel-edit').hidden = true;
}
$('cancel-edit').onclick = cancelEdit;
$('search-form').onsubmit = event => { event.preventDefault(); if (adminBusy) return; trainingPage = 1; adminAction(loadTraining, 'جست‌وجو انجام شد.'); };
$('previous-page').onclick = () => { if (adminBusy) return; trainingPage--; adminAction(loadTraining, 'نمونه‌ها بارگذاری شدند.'); };
$('next-page').onclick = () => { if (adminBusy) return; trainingPage++; adminAction(loadTraining, 'نمونه‌ها بارگذاری شدند.'); };
$('preview-form').onsubmit = event => {
    event.preventDefault(); adminAction(async () => {
        $('preview-result').hidden = true;
        const reply = await api('/api/training/preview', 'POST', { input: $('preview-input').value }, true);
        $('preview-result').textContent = reply.text + '\n' + (reply.matched ? 'پاسخ آموخته‌شده' : 'پاسخ جایگزین؛ نمونه مشابه کافی پیدا نشد') +
            ' · شباهت: ' + Math.round(reply.similarity * 100).toLocaleString('fa-IR') + '٪';
        $('preview-result').hidden = false;
    }, 'آزمایش پایان یافت.');
};
$('import-examples').onclick = () => adminAction(async () => {
    const file = $('import-file').files[0];
    if (!file) throw new Error('ابتدا فایل JSON را انتخاب کنید.');
    if (file.size > 9000000) throw new Error('حجم فایل باید کمتر از ۹ مگابایت باشد.');
    let examples;
    try { examples = JSON.parse(await file.text()); } catch { throw new Error('ساختار فایل JSON معتبر نیست.'); }
    if (!Array.isArray(examples) || !examples.length || examples.length > 500 || examples.some(x =>
        !x || typeof x.input !== 'string' || typeof x.response !== 'string' || !x.input.trim() || !x.response.trim() || x.input.length > 4000 || x.response.length > 8000))
        throw new Error('فایل باید شامل ۱ تا ۵۰۰ نمونه با input (حداکثر ۴۰۰۰ نویسه) و response (حداکثر ۸۰۰۰ نویسه) باشد.');
    await api('/api/training/import', 'POST', { examples }, true);
    $('import-file').value = ''; trainingPage = 1; await loadTraining();
}, 'نمونه‌های فایل ذخیره شدند. اکنون مدل را بازآموزی کنید.');
$('export-examples').onclick = () => adminAction(async () => {
    const examples = await api('/api/training/export', 'GET', undefined, true);
    const url = URL.createObjectURL(new Blob([JSON.stringify(examples, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a'); link.href = url; link.download = 'training-examples.json';
    document.body.append(link); link.click(); link.remove(); setTimeout(() => URL.revokeObjectURL(url), 1000);
}, 'فایل نمونه‌ها آماده شد.');
