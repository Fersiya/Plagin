(function () {
  'use strict';

  const $ = (s) => document.querySelector(s);
  const $$ = (s) => Array.from(document.querySelectorAll(s));

  const APP_VERSION = '1.3.0';
  const REPO = 'Fersiya/Plagin';
  const ASSET = 'SubPanel-Setup.exe';
  const nodeReq = typeof require === 'function' ? require : null; // есть, когда панель запущена с --enable-nodejs

  const KEY_PRESETS = 'subpanel.presets.v1';
  const KEY_STATE = 'subpanel.state.v1';

  const inCEP = !!window.__adobe_cep__;
  const mock = new URLSearchParams(location.search).get('mock'); // ?mock=ae | ?mock=pr — только для просмотра в браузере

  /* ---------- данные ---------- */

  const DEFAULT_STYLE = { font: 'Arial-BoldMT', size: 60, fill: '#ffffff', stroke: '#000000', strokeW: 6, textCase: 'none', align: 'center', tracking: 0, wrap: 0, y: 86 };

  const CENSOR_WORDS = [
    'бля', 'блять', 'блядь', 'сука', 'сучка', 'хуй', 'хуя', 'хуе', 'хуё', 'хули', 'пиздец', 'пизда', 'пидор', 'пидар',
    'ебать', 'ебаный', 'ебучий', 'ебал', 'ёбаный', 'ебло', 'мудак', 'мудила', 'гондон', 'гандон', 'залупа', 'чмо',
    'fuck', 'fucking', 'bitch', 'shit', 'asshole', 'dick', 'cunt', 'bastard', 'whore', 'slut'
  ];

  const BUILTIN = [
    { id: 'b-classic', name: 'Классика', font: 'Arial-BoldMT', size: 60, fill: '#ffffff', stroke: '#000000', strokeW: 6, textCase: 'none', align: 'center', tracking: 0, wrap: 0, y: 86 },
    { id: 'b-shorts', name: 'Шортс', font: 'Impact', size: 96, fill: '#ffe600', stroke: '#000000', strokeW: 12, textCase: 'upper', align: 'center', tracking: 10, wrap: 32, y: 68 },
    { id: 'b-mrbeast', name: 'МистерБист', font: 'Impact', size: 90, fill: '#38ef7d', stroke: '#000000', strokeW: 14, textCase: 'upper', align: 'center', tracking: 5, wrap: 28, y: 50 },
    { id: 'b-tiktok', name: 'ТикТок', font: 'Arial-BoldMT', size: 72, fill: '#ffffff', stroke: '#000000', strokeW: 8, textCase: 'upper', align: 'center', tracking: 5, wrap: 34, y: 82 },
    { id: 'b-cinema', name: 'Кино', font: 'Georgia-Bold', size: 48, fill: '#f4f4f4', stroke: '#000000', strokeW: 2, textCase: 'none', align: 'center', tracking: 25, wrap: 45, y: 90 },
    { id: 'b-neon', name: 'Неон', font: 'Verdana-Bold', size: 64, fill: '#00f0ff', stroke: '#06306b', strokeW: 10, textCase: 'none', align: 'center', tracking: 10, wrap: 36, y: 80 },
    { id: 'b-karaoke', name: 'Караоке', font: 'Arial-BoldMT', size: 78, fill: '#ffffff', stroke: '#7a3cff', strokeW: 14, textCase: 'upper', align: 'center', tracking: 5, wrap: 30, y: 50 },
    { id: 'b-minimal', name: 'Минимал', font: 'ArialMT', size: 44, fill: '#ffffff', stroke: '#000000', strokeW: 0, textCase: 'none', align: 'center', tracking: 0, wrap: 0, y: 90 },
  ];

  const MARKS = [
    { ch: '.', label: '.', on: true },
    { ch: ',', label: ',', on: true },
    { ch: '!', label: '!', on: true },
    { ch: '?', label: '?', on: true },
    { ch: '…', label: '…', on: true },
    { ch: ':', label: ':', on: false },
    { ch: ';', label: ';', on: false },
    { ch: '—–', label: '—', on: false },
    { ch: '-–—', label: '-/—', on: false },
    { ch: '"“”', label: '"', on: false },
    { ch: '«»', label: '«»', on: false },
    { ch: '\'’`', label: '\'', on: false },
    { ch: '()', label: '( )', on: false },
    { ch: '[]', label: '[ ]', on: false },
  ];

  const COMMON_FONTS = [
    'Arial-BoldMT', 'ArialMT', 'Arial-Black', 'Impact', 'Montserrat-Bold', 'Montserrat-Black',
    'Roboto-Bold', 'Roboto-Black', 'BebasNeueBold', 'Inter-Bold', 'Oswald-Bold', 'Verdana',
    'Verdana-Bold', 'Tahoma-Bold', 'SegoeUI', 'SegoeUI-Bold', 'SegoeUI-Black', 'TrebuchetMS-Bold',
    'Calibri-Bold', 'Georgia-Bold', 'TimesNewRomanPS-BoldMT', 'CourierNewPS-BoldMT', 'ComicSansMS-Bold',
  ];

  const MSG = {
    NO_COMP: 'Открой композицию в After Effects.',
    NO_TEXT_LAYERS: 'В композиции нет текстовых слоёв.',
    NO_SEQ: 'Открой секвенцию в Premiere Pro.',
    NO_GRAPHICS: 'Выдели на таймлайне графические клипы с текстом. Подписи (Captions) Premiere скриптам не отдаёт — для них кнопки «Из буфера обмена» и «SRT-файл».',
    NO_PARAMS: 'В выделенных клипах нет параметров шрифта или размера, которые можно изменить.',
    BAD_ACTION: 'Это действие недоступно в текущей программе.',
    BAD_JSON: 'Ошибка передачи данных в хост.',
    IMPORT_FAILED: 'Premiere не смог импортировать файл.',
  };

  /* ---------- состояние ---------- */

  let host = 'UNKNOWN';
  let userPresets = load(KEY_PRESETS, []);
  let st = Object.assign({}, DEFAULT_STYLE, { applyPos: true, instant: false, activeId: 'b-classic', marks: MARKS.filter((m) => m.on).map((m) => m.ch) }, load(KEY_STATE, {}));

  function load(key, fallback) {
    try { const v = JSON.parse(localStorage.getItem(key)); return v == null ? fallback : v; } catch (e) { return fallback; }
  }
  function save() {
    try {
      localStorage.setItem(KEY_PRESETS, JSON.stringify(userPresets));
      localStorage.setItem(KEY_STATE, JSON.stringify(st));
    } catch (e) { /* хранилище может быть недоступно */ }
  }

  /* ---------- связь с хостом ---------- */

  function callHost(action, payload) {
    return new Promise((resolve) => {
      if (!inCEP) {
        resolve({ ok: false, code: 'NOT_CEP' });
        return;
      }
      const lit = JSON.stringify(JSON.stringify(payload || {})).replace(/\u2028/g, '\\u2028').replace(/\u2029/g, '\\u2029');
      const script = 'SP_run(' + JSON.stringify(action) + ',' + lit + ')';
      window.__adobe_cep__.evalScript(script, (res) => {
        try { resolve(JSON.parse(res)); } catch (e) { resolve({ ok: false, code: 'RAW', raw: String(res) }); }
      });
    });
  }

  function describe(r) {
    if (!r.ok) {
      if (MSG[r.code]) return MSG[r.code];
      if (r.code === 'NOT_CEP') return 'Панель открыта вне Adobe — действия работают только внутри Pr или AE.';
      if (r.code === 'RAW') return 'Хост ответил неожиданно: ' + r.raw;
      return 'Ошибка: ' + r.code + (r.line ? ' (строка ' + r.line + ')' : '');
    }
    switch (r.code) {
      case 'applied':
        return 'Стиль применён: ' + r.layers + ' ' + plural(r.layers, 'слой', 'слоя', 'слоёв') +
          (r.scope === 'all' ? ' (ничего не было выделено — взял все текстовые слои)' : '') +
          (r.skipped ? '. Пропущено: ' + r.skipped : '') +
          (r.fontMissing ? '. Шрифта с таким именем нет — оставил прежний' : '');
      case 'stripped':
        return r.removed
          ? 'Убрано знаков: ' + r.removed + ' в ' + r.layers + ' ' + plural(r.layers, 'слое', 'слоях', 'слоях') +
            (r.scope === 'all' ? ' (ничего не было выделено — взял все текстовые слои)' : '') +
            (r.skipped ? '. Пропущено: ' + r.skipped : '')
          : 'Выбранных знаков в тексте нет.';
      case 'pr_applied':
        return 'Применено к клипам: ' + r.clips + ' из ' + r.total + (r.scope === 'all' ? ' (ничего не было выделено — обработал все клипы)' : '') + '.';
      case 'pr_stripped':
        return r.removed
          ? 'Убрано знаков: ' + r.removed + ' в клипах: ' + r.clips + ' из ' + r.total + (r.scope === 'all' ? ' (ничего не было выделено — обработал все клипы)' : '') + '.'
          : 'Выбранных знаков в тексте нет.';
      case 'anim_applied':
        return 'Анимация «' + r.anim + '» применена к слоям: ' + r.layers + (r.skipped ? '. Пропущено: ' + r.skipped : '') + '.';
      case 'censored':
        return 'Цензура применена: обработано ' + (r.layers || r.clips) + ' слоёв/клипов.';
      case 'wrapped':
        return 'Перенос строк выполнен: обработано ' + (r.layers || r.clips) + ' слоёв/клипов.';
      case 'cased':
        return 'Регистр изменен: обработано ' + (r.layers || r.clips) + ' слоёв/клипов.';
      case 'srt_imported':
        return r.track
          ? 'Готово: чистый SRT импортирован и добавлен дорожкой подписей.'
          : 'Чистый SRT импортирован в проект. Перетащи его на таймлайн — автоматически добавить дорожку не вышло.';
      default:
        return 'Готово.';
    }
  }

  function plural(n, a, b, c) {
    const m10 = n % 10, m100 = n % 100;
    if (m10 === 1 && m100 !== 11) return a;
    if (m10 >= 2 && m10 <= 4 && (m100 < 10 || m100 >= 20)) return b;
    return c;
  }

  /* ---------- тост ---------- */

  let toastTimer;
  function toast(text, isErr) {
    const el = $('#toast');
    el.textContent = text;
    el.classList.toggle('err', !!isErr);
    el.classList.add('show');
    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => el.classList.remove('show'), isErr ? 6500 : 3800);
  }

  async function run(btn, fn) {
    btn.disabled = true;
    try { await fn(); } finally { btn.disabled = false; }
  }

  /* ---------- пунктуация ---------- */

  function charsToStrip() {
    return st.marks.join('');
  }

  function stripText(text) {
    const chars = Array.from(charsToStrip());
    let out = String(text);
    if (st.marks.indexOf('…') > -1) {
      out = out.replace(/\.{3,}/g, '');
    }
    for (const ch of chars) out = out.split(ch).join('');
    // Если выбран дефис/тире, убираем также диалоговые тире в начале строк
    if (st.marks.indexOf('-–—') > -1 || st.marks.indexOf('—–') > -1) {
      out = out.replace(/(^|\r?\n)[ \t]*[-–—][ \t]*/g, '$1');
    }
    return out.replace(/[ \t]{2,}/g, ' ').replace(/[ \t]+(\r?\n|$)/g, '$1').replace(/(^|\n)[ \t]+/g, '$1');
  }

  function stripSrt(text) {
    const cleanText = String(text).replace(/^\uFEFF/, '');
    return cleanText.split(/\r?\n/).map((line) => (/^\d+$/.test(line.trim()) || line.indexOf('-->') > -1 ? line : stripText(line))).join('\r\n');
  }

  function applyCase(text, mode) {
    if (text === null || text === undefined || text === '') return text;
    var s = String(text);
    if (!mode || mode === 'none') return s;
    if (mode === 'upper') return s.toUpperCase();
    if (mode === 'lower') return s.toLowerCase();
    if (mode === 'sentence') {
      var lower = s.toLowerCase();
      return lower.replace(/(^|[.!?…\r\n])(\s*["'«“„»”\(\[\{\—\–\-]*\s*)([a-zа-яё])/g, function (m, p1, p2, p3) {
        return p1 + p2 + p3.toUpperCase();
      });
    }
    if (mode === 'title') {
      return s.toLowerCase().replace(/(^|[ \t\r\n"«'“„(\[\{—–-])([a-zа-яё])/g, function (m, p1, p2) {
        return p1 + p2.toUpperCase();
      });
    }
    return s;
  }

  function maskText(text, words, style) {
    if (text === null || text === undefined || text === '') return text;
    if (!words || !words.length) return text;
    var out = String(text);
    var maskPattern = style === 'symbols' ? '@#$%' : '***';
    for (var i = 0; i < words.length; i++) {
      var rawW = words[i];
      if (rawW === null || rawW === undefined) continue;
      var w = String(rawW);
      if (w.length < 2) continue;
      var escW = w.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, '\\$&').replace(/[её]/gi, '[еёЁЕ]');
      try {
        var re = new RegExp('(^|[^a-zA-Zа-яА-ЯёЁ0-9_])(' + escW + ')(?=[^a-zA-Zа-яА-ЯёЁ0-9_]|$)', 'gi');
        out = out.replace(re, function (match, p1, p2) {
          var rep = maskPattern;
          if (maskPattern === '***') {
            rep = '';
            for (var k = 0; k < p2.length; k++) rep += '*';
          }
          return p1 + rep;
        });
      } catch (e) {}
    }
    return out;
  }

  /* ---------- интерфейс ---------- */

  function familyOf(ps) {
    return String(ps || '').split('-')[0].replace(/(MT|PS)$/i, '') || 'Segoe UI';
  }

  function outline(px, color) {
    if (px <= 0) return 'none';
    const r = Math.max(0.5, px);
    const out = [];
    for (let i = 0; i < 16; i++) {
      const a = (i / 16) * Math.PI * 2;
      out.push((Math.cos(a) * r).toFixed(2) + 'px ' + (Math.sin(a) * r).toFixed(2) + 'px 0 ' + color);
    }
    return out.join(',');
  }

  function renderPreview() {
    const frame = $('#frame');
    const sub = $('#sub');
    const scale = frame.clientWidth / 1920;
    sub.style.fontFamily = '"' + familyOf(st.font) + '", "Segoe UI", Arial, sans-serif';
    sub.style.fontWeight = /bold|black|heavy|extra|impact/i.test(st.font) ? '800' : '500';
    sub.style.fontSize = Math.max(7, st.size * scale) + 'px';
    sub.style.color = st.fill;
    sub.style.textShadow = outline((st.strokeW * scale) / 2, st.stroke);
    sub.style.top = st.y + '%';
    sub.style.textAlign = st.align || 'center';
    sub.style.letterSpacing = ((st.tracking || 0) * scale * 0.15).toFixed(1) + 'px';

    var sample = 'Привет, это пример субтитров!';
    if (st.textCase && st.textCase !== 'none') {
      sample = applyCase(sample, st.textCase);
    }
    sub.textContent = sample;
  }

  function chipStyle(aa, p) {
    const scale = 0.17;
    aa.style.fontFamily = '"' + familyOf(p.font) + '", "Segoe UI", Arial, sans-serif';
    aa.style.color = p.fill;
    aa.style.textShadow = outline(Math.min(1.6, (p.strokeW * scale) / 2), p.stroke);
  }

  function renderPresets() {
    const box = $('#presets');
    box.textContent = '';
    const all = BUILTIN.map((p) => Object.assign({ user: false }, p)).concat(userPresets.map((p) => Object.assign({ user: true }, p)));
    for (const p of all) {
      const chip = document.createElement('div');
      chip.className = 'chip' + (p.id === st.activeId ? ' on' : '');
      chip.setAttribute('role', 'button');
      chip.tabIndex = 0;
      const aa = document.createElement('span');
      aa.className = 'aa';
      aa.textContent = 'Aa';
      chipStyle(aa, p);
      const label = document.createElement('span');
      label.textContent = p.name;
      chip.append(aa, label);
      if (p.user) {
        const del = document.createElement('button');
        del.className = 'del';
        del.type = 'button';
        del.title = 'Удалить заготовку';
        del.textContent = '×';
        del.addEventListener('click', (e) => {
          e.stopPropagation();
          userPresets = userPresets.filter((x) => x.id !== p.id);
          if (st.activeId === p.id) st.activeId = null;
          save();
          renderPresets();
        });
        chip.append(del);
      }
      chip.addEventListener('click', () => choosePreset(p));
      box.append(chip);
    }
  }

  function choosePreset(p) {
    st = Object.assign(st, {
      font: p.font,
      size: p.size,
      fill: p.fill,
      stroke: p.stroke,
      strokeW: p.strokeW,
      textCase: p.textCase || 'none',
      align: p.align || 'center',
      tracking: p.tracking || 0,
      wrap: p.wrap || 0,
      y: p.y,
      activeId: p.id
    });
    syncInputs();
    renderPresets();
    renderPreview();
    save();
    if (st.instant) $('#apply').click();
  }

  function renderMarks() {
    const box = $('#marks');
    box.textContent = '';
    for (const m of MARKS) {
      const chip = document.createElement('button');
      chip.type = 'button';
      chip.className = 'chip' + (st.marks.indexOf(m.ch) > -1 ? ' on' : '');
      chip.textContent = m.label;
      chip.addEventListener('click', () => {
        const i = st.marks.indexOf(m.ch);
        if (i > -1) st.marks.splice(i, 1); else st.marks.push(m.ch);
        chip.classList.toggle('on');
        save();
      });
      box.append(chip);
    }
  }

  function syncInputs() {
    $('#font').value = st.font;
    $('#size').value = st.size;
    $('#sizeNum').value = st.size;
    $('#fill').value = st.fill;
    $('#fillHex').textContent = st.fill;
    $('#stroke').value = st.stroke;
    $('#strokeHex').textContent = st.stroke;
    $('#strokeW').value = st.strokeW;
    $('#strokeWNum').value = st.strokeW;
    $('#tracking').value = st.tracking || 0;
    $('#trackingNum').value = st.tracking || 0;
    $('#wrap').value = st.wrap || 0;
    $('#wrapNum').value = st.wrap || 0;
    $('#y').value = st.y;
    $('#yNum').value = st.y;
    $('#applyPos').checked = !!st.applyPos;
    $('#instant').checked = !!st.instant;
    $$('#posSeg button').forEach((b) => b.classList.toggle('on', Number(b.dataset.y) === Number(st.y)));
    $$('#alignSeg button').forEach((b) => b.classList.toggle('on', (b.dataset.align || 'center') === (st.align || 'center')));
    $$('#caseSeg button').forEach((b) => b.classList.toggle('on', (b.dataset.case || 'none') === (st.textCase || 'none')));
  }

  function bind(id, key, parse, after) {
    const el = $(id);
    const ev = el.type === 'checkbox' ? 'change' : 'input';
    el.addEventListener(ev, () => {
      const raw = el.type === 'checkbox' ? el.checked : el.value;
      const v = parse(raw);
      if (v === null) return;
      st[key] = v;
      st.activeId = null;
      if (after) after();
      syncInputs();
      renderPresets();
      renderPreview();
      save();
    });
  }

  function num(min, max) {
    return (raw) => {
      const n = Number(raw);
      if (raw === '' || Number.isNaN(n)) return null;
      return Math.min(max, Math.max(min, Math.round(n)));
    };
  }

  function initFonts(list) {
    const dl = $('#fonts');
    dl.textContent = '';
    const seen = new Set();
    const add = (ps, label) => {
      if (seen.has(ps)) return;
      seen.add(ps);
      const o = document.createElement('option');
      o.value = ps;
      if (label) o.label = label;
      dl.append(o);
    };
    list.forEach((f) => add(f[0], f[1]));
    COMMON_FONTS.forEach((f) => add(f));
  }

  /* ---------- действия ---------- */

  async function onApply() {
    const r = await callHost('apply', {
      font: st.font.trim(),
      size: st.size,
      fill: st.fill,
      stroke: st.stroke,
      strokeW: st.strokeW,
      textCase: st.textCase || 'none',
      align: st.align || 'center',
      tracking: st.tracking || 0,
      maxChars: st.wrap || 0,
      y: st.y,
      applyPos: st.applyPos
    });
    toast(describe(r), !r.ok);
  }

  async function onAnim(type) {
    const r = await callHost('anim', { anim: type });
    toast(describe(r), !r.ok);
  }

  async function onCensor() {
    const style = $('#censorStyle').value || 'stars';
    const r = await callHost('censor', { words: CENSOR_WORDS, style: style });
    toast(describe(r), !r.ok);
  }

  async function onStrip() {
    if (!st.marks.length) { toast('Выбери хотя бы один знак.', true); return; }
    const r = await callHost('strip', { chars: charsToStrip() });
    toast(describe(r), !r.ok);
  }

  async function readClipboard() {
    try {
      if (navigator.clipboard && navigator.clipboard.readText) return await navigator.clipboard.readText();
    } catch (e) { /* пробуем запасной путь */ }
    const ta = $('#manual');
    ta.value = '';
    ta.focus();
    if (document.execCommand('paste') && ta.value) return ta.value;
    return null;
  }

  async function writeClipboard(text) {
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { await navigator.clipboard.writeText(text); return true; }
    } catch (e) { /* пробуем запасной путь */ }
    const ta = $('#manual');
    ta.value = text;
    ta.focus();
    ta.select();
    return document.execCommand('copy');
  }

  async function onClip() {
    if (!st.marks.length) { toast('Выбери хотя бы один знак.', true); return; }
    const text = await readClipboard();
    if (!text) {
      $('details').open = true;
      toast('Буфер недоступен. Вставь текст в поле ниже и нажми «Очистить».', true);
      return;
    }
    const clean = stripText(text);
    const ok = await writeClipboard(clean);
    if (!ok) {
      $('details').open = true;
      $('#manual').value = clean;
      toast('Очистил, но записать в буфер не вышло — скопируй из поля ниже.', true);
      return;
    }
    toast('Готово, буфер очищен. Теперь вставь текст обратно (Ctrl+V).');
  }

  function onSrt() {
    if (!st.marks.length) { toast('Выбери хотя бы один знак.', true); return Promise.resolve(); }
    if (!window.cep || !window.cep.fs) { toast('Выбор файла недоступен.', true); return Promise.resolve(); }
    const dlg = window.cep.fs.showOpenDialogEx(false, false, 'Выбери SRT', '', ['srt']);
    if (!dlg || dlg.err || !dlg.data || !dlg.data.length) return Promise.resolve();
    const src = dlg.data[0];
    const rd = window.cep.fs.readFile(src, window.cep.encoding.UTF8);
    if (rd.err) { toast('Не удалось прочитать файл.', true); return Promise.resolve(); }
    const out = src.replace(/\.srt$/i, '') + '.clean.srt';
    const wr = window.cep.fs.writeFile(out, stripSrt(rd.data), window.cep.encoding.UTF8);
    if (wr.err) { toast('Не удалось сохранить ' + out, true); return Promise.resolve(); }
    return callHost('srt', { path: out }).then((r) => toast(r.ok ? describe(r) : 'Сохранил ' + out + '. ' + describe(r), !r.ok));
  }


  /* ---------- обновления ---------- */

  let update = null;

  function isNewer(a, b) {
    const pa = String(a).replace(/^v/, '').split('.').map((n) => parseInt(n, 10) || 0);
    const pb = String(b).replace(/^v/, '').split('.').map((n) => parseInt(n, 10) || 0);
    for (let i = 0; i < 3; i++) {
      if ((pa[i] || 0) !== (pb[i] || 0)) return (pa[i] || 0) > (pb[i] || 0);
    }
    return false;
  }

  // Качает только с github.com, githubusercontent.com и s3/blob хранилищ релизов по https, редиректы проверяются на каждом шаге.
  function httpsGet(url, accept, hops) {
    hops = hops || 0;
    return new Promise((resolve, reject) => {
      let u;
      try { u = new URL(url); } catch (e) { reject(new Error('bad url')); return; }
      if (u.protocol !== 'https:' || !/(^|\.)(github\.com|githubusercontent\.com|amazonaws\.com)$/.test(u.hostname)) { reject(new Error('host not allowed')); return; }
      const req = nodeReq('https').get(u, { headers: { 'User-Agent': 'SubPanel-updater', Accept: accept } }, (res) => {
        if ([301, 302, 303, 307, 308].indexOf(res.statusCode) > -1 && res.headers.location) {
          res.resume();
          if (hops >= 5) { reject(new Error('too many redirects')); return; }
          resolve(httpsGet(new URL(res.headers.location, u).toString(), accept, hops + 1));
          return;
        }
        const chunks = [];
        res.on('data', (c) => chunks.push(c));
        res.on('end', () => resolve({ status: res.statusCode, body: Buffer.concat(chunks) }));
        res.on('error', reject);
      });
      req.on('error', reject);
      req.setTimeout(30000, () => req.destroy(new Error('timeout')));
    });
  }

  function openUrl(url) {
    try { window.__adobe_cep__.openURLInDefaultBrowser(url); } catch (e) { /* вне Adobe */ }
  }

  async function checkUpdate() {
    try {
      const api = 'https://api.github.com/repos/' + REPO + '/releases/latest';
      let rel;
      if (nodeReq) {
        const r = await httpsGet(api, 'application/vnd.github+json');
        if (r.status !== 200) return;
        rel = JSON.parse(r.body.toString('utf8'));
      } else {
        const r = await fetch(api);
        if (!r.ok) return;
        rel = await r.json();
      }
      if (!rel.tag_name || !isNewer(rel.tag_name, APP_VERSION)) return;
      const asset = (rel.assets || []).find((a) => a.name === ASSET);
      update = {
        version: rel.tag_name.replace(/^v/, ''),
        url: asset ? asset.browser_download_url : null,
        digest: asset && asset.digest ? String(asset.digest) : null,
        page: rel.html_url || 'https://github.com/' + REPO + '/releases/latest',
      };
      $('#updateText').textContent = 'Доступна версия ' + update.version;
      $('#update').hidden = false;
    } catch (e) { /* нет сети или лимит GitHub — молча пропускаем */ }
  }

  async function installUpdate() {
    if (!update) return;
    if (!nodeReq || !update.url) { openUrl(update.page); return; }
    const btn = $('#updateBtn');
    btn.disabled = true;
    btn.textContent = 'Скачиваю…';
    try {
      const r = await httpsGet(update.url, '*/*');
      if (r.status !== 200 || r.body.length < 50000 || r.body[0] !== 0x4d || r.body[1] !== 0x5a) throw new Error('bad download');
      if (update.digest && /^sha256:/i.test(update.digest)) {
        const h = nodeReq('crypto').createHash('sha256').update(r.body).digest('hex');
        if (h !== update.digest.slice(7).toLowerCase()) throw new Error('hash mismatch');
      }
      const file = nodeReq('path').join(nodeReq('os').tmpdir(), 'SubPanel-Setup-' + update.version + '.exe');
      nodeReq('fs').writeFileSync(file, r.body);
      nodeReq('child_process').spawn(file, ['/S'], { detached: true, stdio: 'ignore' }).unref();
      $('#updateText').textContent = 'Версия ' + update.version + ' ставится. Перезапусти Pr/AE через пару секунд.';
      btn.hidden = true;
    } catch (e) {
      btn.disabled = false;
      btn.textContent = 'Обновить';
      toast('Не удалось обновить автоматически — открываю страницу релиза.', true);
      openUrl(update.page);
    }
  }

  /* ---------- запуск ---------- */

  function setHost(name) {
    host = name;
    document.body.classList.remove('pr', 'ae');
    const label = $('#hostName');
    const pill = $('#host');
    pill.classList.remove('pr', 'ae');
    if (name === 'PR') { document.body.classList.add('pr'); pill.classList.add('pr'); label.textContent = 'Premiere Pro'; }
    else if (name === 'AE') { document.body.classList.add('ae'); pill.classList.add('ae'); label.textContent = 'After Effects'; }
    else { label.textContent = inCEP ? 'Неизвестная программа' : 'Просмотр'; }

    $('#applyHint').textContent = name === 'PR'
      ? 'В Premiere стиль применяется к выделенным графическим клипам (текст/MOGRT): шрифт и размер. Цвет, обводка и позиция — только в AE.'
      : 'Применяется к выделенным текстовым слоям. Если ничего не выделено — ко всем текстовым слоям композиции.';
    $('#stripHint').textContent = name === 'PR'
      ? 'Для графических клипов — кнопка выше. Для подписей (Captions): выдели весь текст в панели «Текст», Ctrl+C → «Из буфера обмена» → Ctrl+V.'
      : 'Работает и с субтитрами, собранными в одном слое по ключам.';
  }

  function exportPresets() {
    if (!userPresets.length) {
      toast('Нет сохранённых заготовок для экспорта.', true);
      return;
    }
    try {
      const dataStr = 'data:text/json;charset=utf-8,' + encodeURIComponent(JSON.stringify(userPresets, null, 2));
      const a = document.createElement('a');
      a.setAttribute('href', dataStr);
      a.setAttribute('download', 'subpanel-presets.json');
      document.body.appendChild(a);
      a.click();
      a.remove();
      toast('Заготовки экспортированы в subpanel-presets.json');
    } catch (e) {
      toast('Ошибка экспорта заготовок.', true);
    }
  }

  function importPresets(file) {
    if (!file) return;
    const reader = new FileReader();
    reader.onload = (e) => {
      try {
        const imported = JSON.parse(e.target.result);
        if (!Array.isArray(imported)) throw new Error('not array');
        let added = 0;
        imported.forEach((p) => {
          if (p && p.name && p.font) {
            userPresets.push(Object.assign({}, DEFAULT_STYLE, p, { id: 'u-' + Date.now() + '-' + Math.random().toString(36).substr(2, 4) }));
            added++;
          }
        });
        save();
        renderPresets();
        toast('Импортировано заготовок: ' + added);
      } catch (err) {
        toast('Неверный формат JSON-файла заготовок.', true);
      }
    };
    reader.readAsText(file);
  }

  async function init() {
    bind('#font', 'font', (v) => v, null);
    bind('#size', 'size', num(1, 1000));
    bind('#sizeNum', 'size', num(1, 1000));
    bind('#fill', 'fill', (v) => v);
    bind('#stroke', 'stroke', (v) => v);
    bind('#strokeW', 'strokeW', num(0, 200));
    bind('#strokeWNum', 'strokeW', num(0, 200));
    bind('#tracking', 'tracking', num(-200, 500));
    bind('#trackingNum', 'tracking', num(-200, 500));
    bind('#wrap', 'wrap', num(0, 100));
    bind('#wrapNum', 'wrap', num(0, 100));
    bind('#y', 'y', num(0, 100));
    bind('#yNum', 'y', num(0, 100));
    $('#applyPos').addEventListener('change', (e) => { st.applyPos = e.target.checked; save(); });
    $('#instant').addEventListener('change', (e) => { st.instant = e.target.checked; save(); });

    $$('#posSeg button').forEach((b) => b.addEventListener('click', () => {
      st.y = Number(b.dataset.y);
      st.activeId = null;
      syncInputs(); renderPresets(); renderPreview(); save();
    }));

    $$('#alignSeg button').forEach((b) => b.addEventListener('click', () => {
      st.align = b.dataset.align || 'center';
      st.activeId = null;
      syncInputs(); renderPresets(); renderPreview(); save();
    }));

    $$('#caseSeg button').forEach((b) => b.addEventListener('click', () => {
      st.textCase = b.dataset.case || 'none';
      st.activeId = null;
      syncInputs(); renderPresets(); renderPreview(); save();
    }));

    $('#marksAll').addEventListener('click', () => {
      st.marks = MARKS.map((m) => m.ch);
      renderMarks(); save();
    });
    $('#marksReset').addEventListener('click', () => {
      st.marks = MARKS.filter((m) => m.on).map((m) => m.ch);
      renderMarks(); save();
    });

    $('#exportPreset').addEventListener('click', exportPresets);
    $('#importPreset').addEventListener('click', () => $('#presetFileInput').click());
    $('#presetFileInput').addEventListener('change', (e) => {
      if (e.target.files && e.target.files.length) {
        importPresets(e.target.files[0]);
        e.target.value = '';
      }
    });

    $('#savePreset').addEventListener('click', () => {
      $('#saveRow').hidden = !$('#saveRow').hidden;
      if (!$('#saveRow').hidden) $('#presetName').focus();
    });
    const savePreset = () => {
      const name = $('#presetName').value.trim();
      if (!name) { toast('Введи название заготовки.', true); return; }
      const p = {
        id: 'u-' + Date.now(),
        name,
        font: st.font,
        size: st.size,
        fill: st.fill,
        stroke: st.stroke,
        strokeW: st.strokeW,
        textCase: st.textCase || 'none',
        align: st.align || 'center',
        tracking: st.tracking || 0,
        wrap: st.wrap || 0,
        y: st.y
      };
      userPresets.push(p);
      st.activeId = p.id;
      $('#presetName').value = '';
      $('#saveRow').hidden = true;
      save(); renderPresets();
      toast('Заготовка «' + name + '» сохранена.');
    };
    $('#presetOk').addEventListener('click', savePreset);
    $('#presetName').addEventListener('keydown', (e) => { if (e.key === 'Enter') savePreset(); });

    $('#apply').addEventListener('click', (e) => run(e.currentTarget, onApply));
    $('#strip').addEventListener('click', (e) => run(e.currentTarget, onStrip));
    $('#censorBtn').addEventListener('click', (e) => run(e.currentTarget, onCensor));
    $('#clip').addEventListener('click', (e) => run(e.currentTarget, onClip));
    $('#srt').addEventListener('click', (e) => run(e.currentTarget, onSrt));

    $('#animBounce').addEventListener('click', (e) => run(e.currentTarget, () => onAnim('bounce')));
    $('#animFade').addEventListener('click', (e) => run(e.currentTarget, () => onAnim('fade')));
    $('#animWiggle').addEventListener('click', (e) => run(e.currentTarget, () => onAnim('wiggle')));
    $('#animReset').addEventListener('click', (e) => run(e.currentTarget, () => onAnim('reset')));

    $('#manualClean').addEventListener('click', () => {
      const ta = $('#manual');
      ta.value = stripText(ta.value);
    });
    $('#manualCopy').addEventListener('click', () => {
      const ta = $('#manual');
      ta.focus(); ta.select();
      toast(document.execCommand('copy') ? 'Скопировано.' : 'Не удалось скопировать — нажми Ctrl+C.', false);
    });

    $('#updateBtn').addEventListener('click', installUpdate);
    $('#updateClose').addEventListener('click', () => { $('#update').hidden = true; });
    $('#ver').textContent = 'v' + APP_VERSION;

    window.addEventListener('resize', renderPreview);

    syncInputs();
    renderPresets();
    renderMarks();
    initFonts([]);
    setHost(mock ? mock.toUpperCase().slice(0, 2) === 'PR' ? 'PR' : 'AE' : 'UNKNOWN');
    renderPreview();

    if (inCEP) {
      const h = await callHost('host');
      if (h.ok) setHost(h.host);
      const f = await callHost('fonts');
      if (f.ok && f.fonts && f.fonts.length) initFonts(f.fonts);
    }
    if (inCEP || mock) checkUpdate();
  }

  document.addEventListener('DOMContentLoaded', init);
})();
