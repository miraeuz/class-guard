# Learning browser checks

From the ClassroomGuard project root, with Node.js installed:

```text
npm install --no-save playwright
npx playwright install chromium
node tests/learning/browser.cjs
```

Optional: set `CHROMIUM_EXECUTABLE` to an existing Chromium executable. The test starts a localhost server on port 8765 and writes diagnostic screenshots/log data to `work`. External HTTPS requests are intercepted rather than sent. It checks lesson/quiz data, search, notes and progress, practice scoring, exam answer/deadline persistence, expiry, final-module coverage, math regression, escaped input, duplicate POST prevention, mobile width and JavaScript errors. These are browser checks, not a Windows WPF/service/uninstall acceptance test.
