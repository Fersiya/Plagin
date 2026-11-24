function testCensor(text, words, style) {
  if (text === null || text === undefined || text === '') return text;
  if (!words || !words.length) return text;
  var out = String(text);
  var maskPattern = style === 'symbols' ? '@#$%' : '***';
  for (var i = 0; i < words.length; i++) {
    var rawW = words[i];
    if (rawW === null || rawW === undefined) continue;
    var w = String(rawW);
    if (w.length < 2) continue;
    var escW = w.replace(/[\-\[\]\/\{\}\(\)\*\+\?\.\\\^\$\|]/g, "\\$&").replace(/[её]/gi, '[еёЁЕ]');
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

console.log('1:', testCensor('fuck fuck fuck', ['fuck'], 'stars'));
console.log('2:', testCensor('fuck shit fuck', ['fuck', 'shit'], 'stars'));
console.log('3:', testCensor('«fuck», (fuck)!', ['fuck'], 'stars'));
console.log('4:', testCensor('ЁБАНЫЙ ебаный', ['ёбаный'], 'stars'));
console.log('5:', testCensor(null, ['fuck'], 'stars'));
console.log('6:', testCensor('test 123', [123, null], 'stars'));
console.log('7:', testCensor('сука блядь сука', ['сука', 'блядь'], 'symbols'));

function testCase(text, mode) {
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

console.log('Case 1:', testCase('«привет мир»', 'sentence'));
console.log('Case 2:', testCase('— привет! — как дела?', 'sentence'));
console.log('Case 3:', testCase('“hello world”. “next sentence”!', 'sentence'));
console.log('Case 4:', testCase('строка 1\rстрока 2\nстрока 3', 'sentence'));
console.log('Case 5:', testCase(null, 'upper'));
console.log('Case 6:', testCase('— привет, как дела?', 'title'));

function testWrap(text, maxChars) {
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

console.log('Wrap 1:', JSON.stringify(testWrap('один два три четыре пять\rшесть семь восемь девять десять', 15)));
console.log('Wrap 2:', testWrap(null, 15));
