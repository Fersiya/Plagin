/* SubPanel — host.jsx
 * Общий ExtendScript для Premiere Pro и After Effects. Только ES3, без нестандартных символов:
 * все сообщения для пользователя формирует панель по кодам (code), чтобы не зависеть от кодировки файла.
 */

var SP_HOST = (function () {
  var n = '';
  try { n = String(BridgeTalk.appName).toLowerCase(); } catch (e) {}
  if (n.indexOf('premiere') > -1) return 'PR';
  if (n.indexOf('aftereffects') > -1) return 'AE';
  try { n = String(app.name).toLowerCase(); } catch (e2) {}
  if (n.indexOf('premiere') > -1) return 'PR';
  if (n.indexOf('after effects') > -1) return 'AE';
  return 'UNKNOWN';
})();

/* ---------- JSON ---------- */

function sp_parse(text) {
  var probe = String(text)
    .replace(/\\(?:["\\\/bfnrt]|u[0-9a-fA-F]{4})/g, '@')
    .replace(/"[^"\\\n\r]*"|true|false|null|-?\d+(?:\.\d*)?(?:[eE][+\-]?\d+)?/g, ']')
    .replace(/(?:^|:|,)(?:\s*\[)+/g, '');
  if (/^[\],:{}\s]*$/.test(probe)) {
    return eval('(' + text + ')');
  }
  throw new Error('BAD_JSON');
}

function sp_esc(s) {
  return '"' + String(s).replace(/[\\"\u0000-\u001f\u2028\u2029]/g, function (c) {
    var h = c.charCodeAt(0).toString(16);
    while (h.length < 4) h = '0' + h;
    return '\\u' + h;
  }) + '"';
}

function sp_str(v) {
  var t = typeof v, i, parts, k;
  if (v === null || v === undefined) return 'null';
  if (t === 'number') return isFinite(v) ? String(v) : 'null';
  if (t === 'boolean') return v ? 'true' : 'false';
  if (t === 'string') return sp_esc(v);
  if (v instanceof Array) {
    parts = [];
    for (i = 0; i < v.length; i++) parts.push(sp_str(v[i]));
    return '[' + parts.join(',') + ']';
  }
  parts = [];
  for (k in v) {
    if (v.hasOwnProperty(k)) parts.push(sp_esc(k) + ':' + sp_str(v[k]));
  }
  return '{' + parts.join(',') + '}';
}

/* ---------- общие утилиты ---------- */

function sp_marks(text, chars) {
  var out = String(text), i;
  for (i = 0; i < chars.length; i++) {
    out = out.split(chars.charAt(i)).join('');
  }
  return out;
}

function sp_tidy(text) {
  return String(text)
    .replace(/[ \t]{2,}/g, ' ')
    .replace(/[ \t]+(\r|\n|$)/g, '$1')
    .replace(/(^|\r|\n)[ \t]+/g, '$1');
}

function sp_rgb(hex) {
  var h = String(hex).replace('#', '');
  if (h.length === 3) h = h.charAt(0) + h.charAt(0) + h.charAt(1) + h.charAt(1) + h.charAt(2) + h.charAt(2);
  return [
    parseInt(h.substr(0, 2), 16) / 255,
    parseInt(h.substr(2, 2), 16) / 255,
    parseInt(h.substr(4, 2), 16) / 255
  ];
}

/* ---------- After Effects ---------- */

function ae_comp() {
  var c = null;
  try { c = app.project.activeItem; } catch (e) {}
  if (!c || !(c instanceof CompItem)) throw new Error('NO_COMP');
  return c;
}

function ae_targets(comp) {
  var out = [], sel = comp.selectedLayers, i, l;
  for (i = 0; i < sel.length; i++) {
    if (sel[i] instanceof TextLayer) out.push(sel[i]);
  }
  if (out.length) return { layers: out, scope: 'selected' };
  for (i = 1; i <= comp.numLayers; i++) {
    l = comp.layer(i);
    if (l instanceof TextLayer) out.push(l);
  }
  return { layers: out, scope: 'all' };
}

function ae_srctext(layer) {
  return layer.property('ADBE Text Properties').property('ADBE Text Document');
}

/* Прогоняет fn по всем значениям Source Text: по ключам, если они есть, иначе по текущему значению. */
function ae_each_doc(prop, fn) {
  var n = 0, k, v;
  if (prop.numKeys > 0) {
    for (k = 1; k <= prop.numKeys; k++) {
      v = prop.keyValue(k);
      if (fn(v)) { prop.setValueAtKey(k, v); n++; }
    }
  } else {
    v = prop.value;
    if (fn(v)) { prop.setValue(v); n++; }
  }
  return n;
}

function ae_apply(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, layer, done = 0, skipped = 0, fontMissing = false;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: style');
  try {
    for (i = 0; i < t.layers.length; i++) {
      layer = t.layers[i];
      try {
        ae_each_doc(ae_srctext(layer), function (td) {
          if (d.font) {
            try { td.font = d.font; } catch (ef) { fontMissing = true; }
          }
          if (d.size) td.fontSize = d.size;
          if (d.fill) { td.applyFill = true; td.fillColor = sp_rgb(d.fill); }
          if (d.strokeW > 0) {
            td.applyStroke = true;
            td.strokeColor = sp_rgb(d.stroke || '#000000');
            td.strokeWidth = d.strokeW;
            try { td.strokeOverFill = false; } catch (es) {}
          } else {
            td.applyStroke = false;
          }
          try { td.allCaps = !!d.caps; } catch (ec) {}
          td.justification = ParagraphJustification.CENTER_JUSTIFY;
          return true;
        });
        if (d.applyPos) {
          var pos = layer.property('ADBE Transform Group').property('ADBE Position');
          if (pos.numKeys === 0 && !pos.expressionEnabled) {
            var cur = pos.value;
            var np = [comp.width / 2, comp.height * (d.y / 100)];
            if (cur.length === 3) np.push(cur[2]);
            pos.setValue(np);
          }
        }
        done++;
      } catch (e) {
        skipped++;
      }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'applied', layers: done, skipped: skipped, scope: t.scope, fontMissing: fontMissing };
}

function ae_strip(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, removed = 0, touched = 0, skipped = 0;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: strip');
  try {
    for (i = 0; i < t.layers.length; i++) {
      try {
        var n = ae_each_doc(ae_srctext(t.layers[i]), function (td) {
          var before = td.text;
          var mid = sp_marks(before, d.chars);
          var after = sp_tidy(mid);
          if (after === before) return false;
          removed += before.length - mid.length;
          td.text = after;
          return true;
        });
        if (n) touched++;
      } catch (e) {
        skipped++;
      }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'stripped', removed: removed, layers: touched, skipped: skipped, scope: t.scope };
}

function ae_fonts() {
  var out = [], seen = {}, all, i, j, f, ps;
  try {
    all = app.fonts.allFonts;
    for (i = 0; i < all.length; i++) {
      for (j = 0; j < all[i].length; j++) {
        f = all[i][j];
        ps = f.postScriptName;
        if (ps && !seen[ps]) {
          seen[ps] = 1;
          out.push([ps, f.familyName + ' ' + f.styleName]);
          if (out.length >= 4000) return out;
        }
      }
    }
  } catch (e) {}
  return out;
}

/* ---------- Premiere Pro ---------- */
/* Captions (подписи) Premiere не отдаёт скриптам, поэтому здесь работаем с графическими клипами (текст / MOGRT). */

function pr_seq() {
  var s = null;
  try { s = app.project.activeSequence; } catch (e) {}
  if (!s) throw new Error('NO_SEQ');
  return s;
}

function pr_selected_graphics() {
  var seq = pr_seq(), sel = [], out = [], i, c;
  try { sel = seq.getSelection(); } catch (e) {}
  for (i = 0; i < sel.length; i++) {
    try {
      c = sel[i].getMGTComponent();
      if (c) out.push(c);
    } catch (e2) {}
  }
  return out;
}

function pr_text_param(comp) {
  var ps = comp.properties, n = 0, i, v;
  try { n = (ps.numItems !== undefined) ? ps.numItems : ps.length; } catch (e) {}
  for (i = 0; i < n; i++) {
    try {
      v = ps[i].getValue();
      if (typeof v === 'string' && v.charAt(0) === '{' && v.indexOf('textEditValue') > -1) {
        return { prop: ps[i], obj: sp_parse(v) };
      }
    } catch (e2) {}
  }
  return null;
}

function pr_set_text(param, text) {
  var o = param.obj, runs = o.fontTextRunLength, oldLen = String(o.textEditValue).length, diff = text.length - oldLen, last;
  o.textEditValue = text;
  if (runs instanceof Array && runs.length) {
    last = runs.length - 1;
    runs[last] = Math.max(0, runs[last] + diff);
  }
}

function pr_put(o, keys, val) {
  var i, j, k, changed = 0;
  for (i = 0; i < keys.length; i++) {
    k = keys[i];
    if (o.hasOwnProperty(k)) {
      if (o[k] instanceof Array) {
        for (j = 0; j < o[k].length; j++) o[k][j] = val;
      } else {
        o[k] = val;
      }
      changed++;
    }
  }
  return changed;
}

function pr_strip(d) {
  var comps = pr_selected_graphics(), i, p, before, mid, after, removed = 0, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    before = String(p.obj.textEditValue);
    mid = sp_marks(before, d.chars);
    after = sp_tidy(mid);
    if (after === before) continue;
    removed += before.length - mid.length;
    pr_set_text(p, after);
    p.prop.setValue(sp_str(p.obj), true);
    touched++;
  }
  return { ok: true, code: 'pr_stripped', removed: removed, clips: touched, total: comps.length };
}

function pr_apply(d) {
  var comps = pr_selected_graphics(), i, p, changed, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    changed = 0;
    if (d.size) changed += pr_put(p.obj, ['fontSize', 'fontSizeEditValue'], d.size);
    if (d.font) changed += pr_put(p.obj, ['fontEditValue', 'fontFSName'], d.font);
    if (changed) {
      p.prop.setValue(sp_str(p.obj), true);
      touched++;
    }
  }
  if (!touched) throw new Error('NO_PARAMS');
  return { ok: true, code: 'pr_applied', clips: touched, total: comps.length };
}

function pr_find_item(bin, wanted) {
  var i, it, found, n = bin.children.numItems, mp;
  for (i = 0; i < n; i++) {
    it = bin.children[i];
    try {
      if (it.type === ProjectItemType.BIN) {
        found = pr_find_item(it, wanted);
        if (found) return found;
      } else {
        mp = String(it.getMediaPath()).replace(/\\/g, '/').toLowerCase();
        if (mp === wanted) return it;
      }
    } catch (e) {}
  }
  return null;
}

function pr_import_srt(d) {
  var seq = pr_seq(), path = String(d.path), item, track = false;
  if ($.os.indexOf('Windows') > -1) path = path.replace(/\//g, '\\');
  if (!app.project.importFiles([path], true, app.project.getInsertionBin(), false)) throw new Error('IMPORT_FAILED');
  item = pr_find_item(app.project.rootItem, String(d.path).replace(/\\/g, '/').toLowerCase());
  if (item) {
    try {
      seq.createCaptionTrack(item, 0, Sequence.CAPTION_FORMAT_SUBTITLE);
      track = true;
    } catch (e) {}
  }
  return { ok: true, code: 'srt_imported', track: track };
}

/* ---------- вход из панели ---------- */

function SP_run(action, payload) {
  var r;
  try {
    var d = payload ? sp_parse(payload) : {};
    if (action === 'host') {
      r = { ok: true, host: SP_HOST };
    } else if (action === 'fonts') {
      r = { ok: true, fonts: SP_HOST === 'AE' ? ae_fonts() : [] };
    } else if (SP_HOST === 'AE' && action === 'apply') {
      r = ae_apply(d);
    } else if (SP_HOST === 'AE' && action === 'strip') {
      r = ae_strip(d);
    } else if (SP_HOST === 'PR' && action === 'apply') {
      r = pr_apply(d);
    } else if (SP_HOST === 'PR' && action === 'strip') {
      r = pr_strip(d);
    } else if (SP_HOST === 'PR' && action === 'srt') {
      r = pr_import_srt(d);
    } else {
      throw new Error('BAD_ACTION');
    }
  } catch (e) {
    r = { ok: false, code: String(e.message || e), line: e.line || 0 };
  }
  return sp_str(r);
}
