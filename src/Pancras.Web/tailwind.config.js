/** @type {import('tailwindcss').Config} */
module.exports = {
  content: [
    './Pages/**/*.{cshtml,cs}',
    './Formatting/**/*.cs',
    './Catalog/**/*.cs',
    './Security/**/*.cs',
    './wwwroot/js/**/*.js'
  ],
  theme: {
    extend: {
      fontFamily: {
        sans: ['DM Sans', 'sans-serif'],
        display: ['Plus Jakarta Sans', 'sans-serif'],
        mono: ['JetBrains Mono', 'monospace']
      }
    }
  }
};
