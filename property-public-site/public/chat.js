const config = await fetch('/site-config').then(response => response.json()).catch(() => null);
if (config?.chatEnabled) mountAssistant(config);

function element(tag, className, text) {
  const node = document.createElement(tag);
  if (className) node.className = className;
  if (text) node.textContent = text;
  return node;
}
function mountAssistant(config) {
  let history = []; let busy = false; let previouslyFocused;
  const root = element('aside', 'property-assistant');
  const launcher = element('button', 'chat-launcher', 'Ask about a property');
  launcher.type = 'button'; launcher.setAttribute('aria-expanded', 'false'); launcher.setAttribute('aria-controls', 'property-chat');
  const panel = element('section', 'chat-panel'); panel.id = 'property-chat'; panel.hidden = true;
  panel.setAttribute('role', 'dialog'); panel.setAttribute('aria-label', `${config.agencyName} property assistant`);
  const heading = element('div', 'chat-heading');
  const title = element('h2', '', `${config.agencyName} assistant`);
  const reset = element('button', 'plain-button', 'New chat'); reset.type = 'button';
  const close = element('button', 'plain-button', 'Close'); close.type = 'button';
  heading.append(title, reset, close);
  const notice = element('p', 'chat-notice', 'AI assistant · Answers use approved agency listings. Messages are processed by OpenAI. Check details with the agency.');
  const messages = element('div', 'chat-messages'); messages.setAttribute('role', 'log'); messages.setAttribute('aria-live', 'polite'); messages.setAttribute('aria-relevant', 'additions');
  const status = element('p', 'chat-status'); status.setAttribute('role', 'status');
  const form = element('form', 'chat-compose');
  const label = element('label', 'sr-only', 'Your property question'); label.htmlFor = 'chat-question';
  const input = element('textarea'); input.id = 'chat-question'; input.rows = 2; input.maxLength = 2000; input.required = true; input.placeholder = 'Area, property type and budget…';
  const submit = element('button', 'button primary small', 'Send'); submit.type = 'submit';
  form.append(label, input, submit); panel.append(heading, notice, messages, status, form); root.append(launcher, panel); document.body.append(root);
  function addMessage(role, text) {
    const message = element('div', `chat-message chat-${role}`);
    message.append(element('strong', '', role === 'user' ? 'You' : 'Assistant'), element('p', '', text));
    messages.append(message); messages.scrollTop = messages.scrollHeight;
    return message;
  }
  function welcome() { addMessage('assistant', 'Hello! Tell me where you’re looking, whether you want to buy or rent, and your budget. I can also help with rooms, property details and enquiries.'); }
  function hide() { panel.hidden = true; launcher.setAttribute('aria-expanded', 'false'); previouslyFocused?.focus(); }
  launcher.addEventListener('click', function toggle() {
    if (!panel.hidden) return hide();
    previouslyFocused = document.activeElement; panel.hidden = false; launcher.setAttribute('aria-expanded', 'true'); input.focus();
  });
  close.addEventListener('click', hide);
  panel.addEventListener('keydown', function escape(event) { if (event.key === 'Escape') hide(); });
  reset.addEventListener('click', function restart() {
    if (busy) return;
    history = []; messages.replaceChildren(); status.textContent = ''; welcome(); input.focus();
  });
  form.addEventListener('submit', async function ask(event) {
    event.preventDefault(); if (busy || !input.value.trim()) return;
    const content = input.value.trim(); input.value = ''; addMessage('user', content);
    history.push({ role: 'user', content }); history = history.slice(-11);
    busy = true; submit.disabled = true; reset.disabled = true; status.textContent = 'Checking agency listings…';
    // A new question replaces any pending enquiry review, so obsolete drafts cannot be submitted accidentally.
    for (const review of messages.querySelectorAll('.chat-enquiry')) review.remove();
    try {
      const result = await post('/api/chat', { messages: history });
      history.push({ role: 'assistant', content: result.message.slice(0, 2000) });
      const message = addMessage('assistant', result.message);
      for (const property of result.properties || []) message.append(propertyCard(property));
      if (result.enquiry) message.append(enquiryReview(result.enquiry));
      status.textContent = '';
    } catch (error) { addMessage('assistant', error.message); status.textContent = 'You can retry your question.'; }
    finally { busy = false; submit.disabled = false; reset.disabled = false; messages.scrollTop = messages.scrollHeight; }
  });
  function enquiryReview(draft) {
    const review = element('section', 'chat-enquiry'); review.append(element('h3', '', 'Review your enquiry'));
    for (const [label, value] of [['Purpose', draft.listingType === 'Sale' ? 'Buying' : 'Renting'], ['Property', draft.address], ['Name', draft.name], ['Surname', draft.surname], ['Email', draft.email], ['Cellphone', draft.phone], ['Message', draft.message]]) {
      if (value) review.append(element('p', '', `${label}: ${value}`));
    }
    review.append(element('p', 'chat-notice', `Confirm to share these details with ${config.agencyName}. To change them, cancel and tell the assistant what to update.`));
    const confirm = element('button', 'button primary small', 'Confirm and send'); confirm.type = 'button';
    const cancel = element('button', 'plain-button', 'Cancel'); cancel.type = 'button';
    const feedback = element('p'); feedback.setAttribute('role', 'status');
    cancel.addEventListener('click', function cancelEnquiry() { if (!busy) review.remove(); });
    confirm.addEventListener('click', async function confirmEnquiry() {
      if (busy) return;
      busy = true; confirm.disabled = true; cancel.disabled = true; submit.disabled = true; reset.disabled = true; feedback.textContent = 'Sending your enquiry…';
      try {
        const result = await post('/api/chat/enquiry', { token: draft.token, confirmed: true });
        feedback.textContent = `${result.message} Reference: ${result.reference}.`;
        confirm.remove(); cancel.remove();
        history.push({ role: 'assistant', content: 'The visitor confirmed their enquiry and the website successfully sent it to the agency.' });
      } catch (error) { feedback.textContent = error.message; confirm.disabled = false; cancel.disabled = false; }
      finally { busy = false; submit.disabled = false; reset.disabled = false; }
    });
    review.append(confirm, cancel, feedback); return review;
  }
  function propertyCard(property) {
    const link = element('a', 'chat-property'); link.href = `/properties/${Number(property.id)}`;
    if (safeImage(property.thumb)) { const image = element('img'); image.src = property.thumb; image.alt = ''; image.loading = 'lazy'; image.referrerPolicy = 'no-referrer'; link.append(image); }
    const details = element('span'); details.append(element('strong', '', property.address), element('span', '', property.propertyType));
    const amount = property.isForSale ? property.salePrice : property.rentalPrice;
    const price = amount == null ? 'Price on request' : new Intl.NumberFormat(undefined, { style: 'currency', currency: config.currency || 'ZAR', maximumFractionDigits: 0 }).format(amount);
    details.append(element('span', '', price + (!property.isForSale && property.isForRent ? ' / month' : ''))); link.append(details); return link;
  }
  welcome();
}
function safeImage(value) { return typeof value === 'string' && (/^data:image\/(jpeg|png|webp);base64,[a-zA-Z0-9+/=]+$/.test(value) || /^https:\/\//.test(value) || /^\/(?!\/)/.test(value)); }
async function post(url, body) {
  const response = await fetch(url, { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(body), signal: AbortSignal.timeout(90000) });
  const data = await response.json();
  if (!response.ok) throw new Error(data.message || 'Unable to complete your request. Please try again.');
  return data;
}
