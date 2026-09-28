---
name: web-project
description: Build a beginner-friendly website or web app (HTML, CSS, JavaScript, no build step), run it locally, test it, and optionally publish it.
---
# Web project

## Structure
```
<project>/
  index.html      the page (or one page per file: about.html, ...)
  styles.css      all styles
  script.js       behaviour (loaded with <script src="script.js" defer>)
  images/         pictures, with meaningful file names
```
Start small: one page that works end to end, then add sections.

## Conventions
- Semantic HTML: header, nav, main, section, footer; one h1 per page.
- Mobile first: `<meta name="viewport" content="width=device-width, initial-scale=1">`,
  flexible layouts (flexbox/grid), images with `max-width: 100%`.
- Accessible: every image has alt text, form fields have labels, text contrast is high,
  buttons are real `<button>` elements.
- Colours and fonts as CSS variables at the top of styles.css so the student can change
  the look in one place.
- JavaScript: `const`/`let`, small named functions, comments on anything not obvious;
  no framework unless the student asks.

## Run and test
- Open `index.html` in the browser, or serve the folder: `python -m http.server 8000`
  then http://localhost:8000 (needed for fetch() of local files).
- Check: the browser console has no errors, every link and image loads, the page works on
  a narrow window (phone size) and a wide one.

## Publish (only if the student wants it)
Offer, with the `question` tool: keep it on their computer, GitHub Pages (free, needs a
GitHub account), or Netlify Drop (drag the folder onto app.netlify.com/drop). Walk them
through their choice step by step.
