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
  if (typeof JSON !== 'undefined' && JSON.parse) {
    try { return JSON.parse(text); } catch (ej) {}
  }
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
  if (typeof JSON !== 'undefined' && JSON.stringify) {
    try { return JSON.stringify(v); } catch (ej) {}
  }
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

function sp_case(text, mode) {
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

function sp_wrap(text, maxChars) {
  if (text === null || text === undefined || text === '') return text;
  if (!maxChars || maxChars < 4) return text;
  var lines = String(text).split(/\r\n|[\r\n]/);
  var outLines = [];
  for (var i = 0; i < lines.length; i++) {
    var line = lines[i];
    if (line.length <= maxChars) {
      outLines.push(line);
      continue;
    }
    var words = line.split(/[ \t]+/);
    var cur = '';
    for (var w = 0; w < words.length; w++) {
      var word = words[w];
      if (!cur) {
        cur = word;
      } else if ((cur + ' ' + word).length <= maxChars) {
        cur += ' ' + word;
      } else {
        outLines.push(cur);
        cur = word;
      }
    }
    if (cur) outLines.push(cur);
  }
  return outLines.join('\n');
}

function sp_censor(text, words, style) {
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
      if (layer.locked) {
        skipped++;
        continue;
      }
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
          if (d.textCase && d.textCase !== 'none') {
            td.text = sp_case(td.text, d.textCase);
          }
          if (d.maxChars && d.maxChars > 0) {
            td.text = sp_wrap(td.text, d.maxChars);
          }
          if (d.tracking !== undefined && d.tracking !== null) {
            try { td.tracking = Number(d.tracking); } catch (etr) {}
          }
          if (d.align === 'left') {
            td.justification = ParagraphJustification.LEFT_JUSTIFY;
          } else if (d.align === 'right') {
            td.justification = ParagraphJustification.RIGHT_JUSTIFY;
          } else {
            td.justification = ParagraphJustification.CENTER_JUSTIFY;
          }
          return true;
        });
        if (d.applyPos) {
          var xTarget = comp.width / 2;
          if (d.align === 'left') xTarget = comp.width * 0.15;
          else if (d.align === 'right') xTarget = comp.width * 0.85;

          var pos = layer.property('ADBE Transform Group').property('ADBE Position');
          if (pos) {
            if (pos.dimensionsSeparated) {
              var posX = layer.property('ADBE Transform Group').property('ADBE Position_0');
              var posY = layer.property('ADBE Transform Group').property('ADBE Position_1');
              if (posX && posY && posX.numKeys === 0 && posY.numKeys === 0 && !posX.expressionEnabled && !posY.expressionEnabled) {
                posX.setValue(xTarget);
                posY.setValue(comp.height * (d.y / 100));
              }
            } else if (pos.numKeys === 0 && !pos.expressionEnabled) {
              var cur = pos.value;
              var np = [xTarget, comp.height * (d.y / 100)];
              if (cur.length === 3) np.push(cur[2]);
              pos.setValue(np);
            }
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
      if (t.layers[i].locked) {
        skipped++;
        continue;
      }
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

function ae_anim(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, layer, done = 0, skipped = 0;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: animation');
  try {
    for (i = 0; i < t.layers.length; i++) {
      layer = t.layers[i];
      if (layer.locked) { skipped++; continue; }
      try {
        var transform = layer.property('ADBE Transform Group');
        var scaleProp = transform.property('ADBE Scale');
        var opacityProp = transform.property('ADBE Opacity');
        var posProp = transform.property('ADBE Position');
        var t0 = layer.inPoint;
        var fd = comp.frameDuration;

        function clearPropKeys(prop) {
          if (!prop || !prop.canVaryOverTime) return;
          for (var k = prop.numKeys; k >= 1; k--) prop.removeKey(k);
          try { if (prop.expressionEnabled) prop.expression = ''; } catch (ee) {}
        }

        if (d.anim === 'reset') {
          clearPropKeys(scaleProp);
          scaleProp.setValue([100, 100, 100]);
          clearPropKeys(opacityProp);
          opacityProp.setValue(100);
          if (posProp.dimensionsSeparated) {
            clearPropKeys(transform.property('ADBE Position_0'));
            clearPropKeys(transform.property('ADBE Position_1'));
          } else {
            clearPropKeys(posProp);
          }
        } else if (d.anim === 'bounce') {
          clearPropKeys(scaleProp);
          var t1 = t0, t2 = t0 + 4 * fd, t3 = t0 + 7 * fd;
          scaleProp.setValueAtTime(t1, [80, 80, 100]);
          scaleProp.setValueAtTime(t2, [105, 105, 100]);
          scaleProp.setValueAtTime(t3, [100, 100, 100]);

          var k1 = scaleProp.nearestKeyIndex(t1);
          var k2 = scaleProp.nearestKeyIndex(t2);
          var k3 = scaleProp.nearestKeyIndex(t3);

          function ease3D(spd, infl) {
            return [new KeyframeEase(spd, infl), new KeyframeEase(spd, infl), new KeyframeEase(spd, infl)];
          }

          scaleProp.setInterpolationTypeAtKey(k1, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
          scaleProp.setInterpolationTypeAtKey(k2, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
          scaleProp.setInterpolationTypeAtKey(k3, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);

          scaleProp.setTemporalEaseAtKey(k1, ease3D(0, 33.33), ease3D(0, 40));
          scaleProp.setTemporalEaseAtKey(k2, ease3D(0, 40), ease3D(0, 40));
          scaleProp.setTemporalEaseAtKey(k3, ease3D(0, 65), ease3D(0, 33.33));
        } else if (d.anim === 'fade') {
          clearPropKeys(opacityProp);
          var tEnd = t0 + 7 * fd;
          opacityProp.setValueAtTime(t0, 0);
          opacityProp.setValueAtTime(tEnd, 100);
          var kOp1 = opacityProp.nearestKeyIndex(t0);
          var kOp2 = opacityProp.nearestKeyIndex(tEnd);
          opacityProp.setInterpolationTypeAtKey(kOp1, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
          opacityProp.setInterpolationTypeAtKey(kOp2, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
          var ease1D = [new KeyframeEase(0, 45)];
          opacityProp.setTemporalEaseAtKey(kOp1, [new KeyframeEase(0, 33.33)], ease1D);
          opacityProp.setTemporalEaseAtKey(kOp2, ease1D, [new KeyframeEase(0, 33.33)]);

          if (posProp.dimensionsSeparated) {
            var yProp = transform.property('ADBE Position_1');
            clearPropKeys(yProp);
            var curY = yProp.value;
            yProp.setValueAtTime(t0, curY + 30);
            yProp.setValueAtTime(tEnd, curY);
            var kY1 = yProp.nearestKeyIndex(t0);
            var kY2 = yProp.nearestKeyIndex(tEnd);
            yProp.setInterpolationTypeAtKey(kY1, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
            yProp.setInterpolationTypeAtKey(kY2, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
            yProp.setTemporalEaseAtKey(kY1, [new KeyframeEase(0, 33.33)], [new KeyframeEase(0, 40)]);
            yProp.setTemporalEaseAtKey(kY2, [new KeyframeEase(0, 65)], [new KeyframeEase(0, 33.33)]);
          } else {
            clearPropKeys(posProp);
            var curPos = posProp.value;
            var startPos = curPos.slice(0);
            startPos[1] += 30;
            posProp.setValueAtTime(t0, startPos);
            posProp.setValueAtTime(tEnd, curPos);
            var kp1 = posProp.nearestKeyIndex(t0);
            var kp2 = posProp.nearestKeyIndex(tEnd);
            posProp.setSpatialContinuousAtKey(kp1, false);
            posProp.setSpatialAutoBezierAtKey(kp1, false);
            posProp.setSpatialTangentsAtKey(kp1, [0, 0, 0], [0, 0, 0]);
            posProp.setSpatialContinuousAtKey(kp2, false);
            posProp.setSpatialAutoBezierAtKey(kp2, false);
            posProp.setSpatialTangentsAtKey(kp2, [0, 0, 0], [0, 0, 0]);
            var dims = curPos.length;
            var pEaseIn = [], pEaseOut = [];
            for (var dm = 0; dm < dims; dm++) {
              pEaseIn.push(new KeyframeEase(0, 65));
              pEaseOut.push(new KeyframeEase(0, 40));
            }
            posProp.setInterpolationTypeAtKey(kp1, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
            posProp.setInterpolationTypeAtKey(kp2, KeyframeInterpolationType.BEZIER, KeyframeInterpolationType.BEZIER);
            posProp.setTemporalEaseAtKey(kp1, pEaseIn, pEaseOut);
            posProp.setTemporalEaseAtKey(kp2, pEaseIn, pEaseOut);
          }
        } else if (d.anim === 'wiggle') {
          posProp.expression = 'wiggle(2, 6);';
        }
        done++;
      } catch (err) {
        skipped++;
      }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'anim_applied', layers: done, skipped: skipped, anim: d.anim, scope: t.scope };
}

function ae_censor(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, touched = 0, skipped = 0;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: censor');
  try {
    for (i = 0; i < t.layers.length; i++) {
      if (t.layers[i].locked) { skipped++; continue; }
      try {
        var n = ae_each_doc(ae_srctext(t.layers[i]), function (td) {
          var before = td.text;
          var after = sp_censor(before, d.words, d.style);
          if (after === before) return false;
          td.text = after;
          return true;
        });
        if (n) touched++;
      } catch (e) { skipped++; }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'censored', layers: touched, skipped: skipped, scope: t.scope };
}

function ae_wrap(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, touched = 0, skipped = 0;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: wrap');
  try {
    for (i = 0; i < t.layers.length; i++) {
      if (t.layers[i].locked) { skipped++; continue; }
      try {
        var n = ae_each_doc(ae_srctext(t.layers[i]), function (td) {
          var before = td.text;
          var after = sp_wrap(before, d.maxChars);
          if (after === before) return false;
          td.text = after;
          return true;
        });
        if (n) touched++;
      } catch (e) { skipped++; }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'wrapped', layers: touched, skipped: skipped, scope: t.scope };
}

function ae_case(d) {
  var comp = ae_comp(), t = ae_targets(comp), i, touched = 0, skipped = 0;
  if (!t.layers.length) throw new Error('NO_TEXT_LAYERS');
  app.beginUndoGroup('SubPanel: case');
  try {
    for (i = 0; i < t.layers.length; i++) {
      if (t.layers[i].locked) { skipped++; continue; }
      try {
        var n = ae_each_doc(ae_srctext(t.layers[i]), function (td) {
          var before = td.text;
          var after = sp_case(before, d.mode);
          if (after === before) return false;
          td.text = after;
          return true;
        });
        if (n) touched++;
      } catch (e) { skipped++; }
    }
  } finally {
    app.endUndoGroup();
  }
  return { ok: true, code: 'cased', layers: touched, skipped: skipped, scope: t.scope };
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

function pr_targets() {
  var seq = pr_seq(), sel = [], out = [], i, t, c, clip, comp;
  try { sel = seq.getSelection(); } catch (e) {}
  for (i = 0; i < sel.length; i++) {
    try {
      comp = sel[i].getMGTComponent();
      if (comp) out.push(comp);
    } catch (e2) {}
  }
  if (out.length) return { comps: out, scope: 'selected' };

  // Если ничего не выделено — сканируем все видеодорожки активной секвенции
  try {
    for (t = 0; t < seq.videoTracks.numTracks; t++) {
      var trk = seq.videoTracks[t];
      for (c = 0; c < trk.clips.numItems; c++) {
        clip = trk.clips[c];
        try {
          comp = clip.getMGTComponent();
          if (comp) out.push(comp);
        } catch (e3) {}
      }
    }
  } catch (e4) {}
  return { comps: out, scope: 'all' };
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
  var t = pr_targets(), comps = t.comps, i, p, before, mid, after, removed = 0, touched = 0;
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
  return { ok: true, code: 'pr_stripped', removed: removed, clips: touched, total: comps.length, scope: t.scope };
}

function pr_censor(d) {
  var t = pr_targets(), comps = t.comps, i, p, before, after, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    before = String(p.obj.textEditValue);
    after = sp_censor(before, d.words, d.style);
    if (after === before) continue;
    pr_set_text(p, after);
    p.prop.setValue(sp_str(p.obj), true);
    touched++;
  }
  return { ok: true, code: 'censored', clips: touched, total: comps.length, scope: t.scope };
}

function pr_wrap(d) {
  var t = pr_targets(), comps = t.comps, i, p, before, after, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    before = String(p.obj.textEditValue);
    after = sp_wrap(before, d.maxChars);
    if (after === before) continue;
    pr_set_text(p, after);
    p.prop.setValue(sp_str(p.obj), true);
    touched++;
  }
  return { ok: true, code: 'wrapped', clips: touched, total: comps.length, scope: t.scope };
}

function pr_case(d) {
  var t = pr_targets(), comps = t.comps, i, p, before, after, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    before = String(p.obj.textEditValue);
    after = sp_case(before, d.mode);
    if (after === before) continue;
    pr_set_text(p, after);
    p.prop.setValue(sp_str(p.obj), true);
    touched++;
  }
  return { ok: true, code: 'cased', clips: touched, total: comps.length, scope: t.scope };
}

function pr_apply(d) {
  var t = pr_targets(), comps = t.comps, i, p, changed, touched = 0;
  if (!comps.length) throw new Error('NO_GRAPHICS');
  for (i = 0; i < comps.length; i++) {
    p = pr_text_param(comps[i]);
    if (!p) continue;
    changed = 0;
    if (d.size) changed += pr_put(p.obj, ['fontSize', 'fontSizeEditValue'], d.size);
    if (d.font) changed += pr_put(p.obj, ['fontEditValue', 'fontFSName'], d.font);
    if (d.align) {
      var alignNum = d.align === 'left' ? 0 : (d.align === 'right' ? 2 : 1);
      changed += pr_put(p.obj, ['justification', 'alignment', 'paraAlignment', 'paragraphJustification'], alignNum);
    }
    if (d.caps !== undefined) {
      changed += pr_put(p.obj, ['allCaps', 'isAllCaps', 'capitalization'], d.caps ? 1 : 0);
    }
    if (d.textCase && d.textCase !== 'none') {
      var curTxt = String(p.obj.textEditValue);
      var cased = sp_case(curTxt, d.textCase);
      if (cased !== curTxt) {
        pr_set_text(p, cased);
        changed++;
      }
    }
    if (d.maxChars && d.maxChars > 0) {
      var curT = String(p.obj.textEditValue);
      var wr = sp_wrap(curT, d.maxChars);
      if (wr !== curT) {
        pr_set_text(p, wr);
        changed++;
      }
    }
    if (changed) {
      p.prop.setValue(sp_str(p.obj), true);
      touched++;
    }
  }
  if (!touched) throw new Error('NO_PARAMS');
  return { ok: true, code: 'pr_applied', clips: touched, total: comps.length, scope: t.scope };
}

function pr_find_item(bin, wanted) {
  var i, it, found, n = bin.children.numItems, mp, nm;
  var wantedBase = wanted.substring(wanted.lastIndexOf('/') + 1).toLowerCase();
  for (i = 0; i < n; i++) {
    it = bin.children[i];
    try {
      if (it.type === ProjectItemType.BIN) {
        found = pr_find_item(it, wanted);
        if (found) return found;
      } else {
        mp = String(it.getMediaPath()).replace(/\\/g, '/').toLowerCase();
        nm = String(it.name).toLowerCase();
        if (mp === wanted || nm === wantedBase) return it;
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
      var format = (typeof Sequence !== 'undefined' && Sequence.CAPTION_FORMAT_SUBTITLE !== undefined) ? Sequence.CAPTION_FORMAT_SUBTITLE : 0;
      seq.createCaptionTrack(item, 0, format);
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
    } else if (SP_HOST === 'AE' && action === 'anim') {
      r = ae_anim(d);
    } else if (SP_HOST === 'AE' && action === 'censor') {
      r = ae_censor(d);
    } else if (SP_HOST === 'AE' && action === 'wrap') {
      r = ae_wrap(d);
    } else if (SP_HOST === 'AE' && action === 'case') {
      r = ae_case(d);
    } else if (SP_HOST === 'PR' && action === 'apply') {
      r = pr_apply(d);
    } else if (SP_HOST === 'PR' && action === 'strip') {
      r = pr_strip(d);
    } else if (SP_HOST === 'PR' && action === 'censor') {
      r = pr_censor(d);
    } else if (SP_HOST === 'PR' && action === 'wrap') {
      r = pr_wrap(d);
    } else if (SP_HOST === 'PR' && action === 'case') {
      r = pr_case(d);
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
