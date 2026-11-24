const fs = require("fs");
const path = require("path");

const hostPath = path.join(__dirname, "..", "jsx", "host.jsx");
const code = fs.readFileSync(hostPath, "utf8");

const vm = require("vm");
const sandbox = {
  app: {},
  BridgeTalk: { appName: "premiere" },
  console: console
};
vm.createContext(sandbox);

vm.runInContext(code, sandbox);

console.log("=== Testing sp_case ===");
console.log("sentence:", sandbox.sp_case("привет мир! как дела? ВСЕ ХОРОШО. а это новая строка.", "sentence"));
console.log("title:", sandbox.sp_case("привет мир! как дела? «крутое видео»", "title"));
console.log("upper:", sandbox.sp_case("привет мир", "upper"));
console.log("lower:", sandbox.sp_case("ПРИВЕТ МИР", "lower"));

console.log("\n=== Testing sp_wrap ===");
const longText = "Это очень длинный текст субтитров который обязательно нужно разбить на строки для вертикального видео";
console.log(sandbox.sp_wrap(longText, 30));

console.log("\n=== Testing sp_censor ===");
console.log(sandbox.sp_censor("Это просто пиздец какой-то, блять, а не работа!", ["пиздец", "блять"], "stars"));
console.log(sandbox.sp_censor("What the fuck is this shit?", ["fuck", "shit"], "symbols"));
console.log(sandbox.sp_censor("употреблять - не должно цензурироваться", ["ебать", "бля"], "stars"));
