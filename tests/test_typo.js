function sp_typo(text) {
  if (text === null || text === undefined || text === '') return text;
  var s = String(text);

  // 1. Multiple spaces -> single space
  s = s.replace(/[ \t]{2,}/g, ' ');

  // 2. Dashes: " -- " or " - " between words -> em-dash " — "
  s = s.replace(/(\s)-{1,2}(\s)/g, '$1—$2');
  // Dialogue dash at start of line: "- " or "-- " -> "— "
  s = s.replace(/(^|\r\n|[\r\n])-{1,2}\s+/g, '$1— ');

  // 3. Ellipsis: "..." -> "…"
  s = s.replace(/\.{3}/g, '…');

  // 4. Russian quotes: convert straight quotes "text" into «text»
  var inQuote = false;
  var chars = s.split('');
  for (var i = 0; i < chars.length; i++) {
    if (chars[i] === '"') {
      if (!inQuote) {
        chars[i] = '«';
        inQuote = true;
      } else {
        chars[i] = '»';
        inQuote = false;
      }
    }
  }
  s = chars.join('');

  // 5. Non-breaking spaces (\u00A0) after short Russian prepositions/conjunctions
  // so they don't dangle alone at line ends
  var preps = 'в|на|с|и|к|о|по|из|от|у|за|под|над|для|не|но|что|как|а|то|же|ли';
  var rePrep = new RegExp('(^|[\\s«\"\\(\\[])(' + preps + ')\\s+', 'gi');
  s = s.replace(rePrep, '$1$2\u00A0');

  return s;
}

const input = '- привет - это крутой "текст в кавычках"... Мы идем в кино с друзьями!';
const out = sp_typo(input);
console.log('Result:', out);
console.log('Has non-breaking space after в:', out.indexOf('в\u00A0') > -1);
console.log('Has em-dash:', out.indexOf('—') > -1);
console.log('Has Russian quotes:', out.indexOf('«текст в\u00A0кавычках»') > -1 || out.indexOf('«текст') > -1);
