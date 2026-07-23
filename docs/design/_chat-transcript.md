# Unity Installer Pages

_Started 2026-05-23 14:06 UTC_

---

## User

я згенерував зображення мого інсталера для юніті. ти можеш розпарсити зображення і зробити прям осторінками, згенерувавши чи підставивши іконки. це бдуе в юніті в едіторі врахуй

## Assistant

_[tool: view_image]_

_[tool: copy_starter_component]_

Я бачу 8 екранів інсталера в стилі Unity Editor. Зроблю кожен як окремий артборд на полотні дизайну, з власноруч намальованими іконками в стилі Unity dark theme. Розпарсюю весь флоу — від welcome до update-redirect.

Зазначу: оскільки оригінальне зображення — це згенерований mockup вашого інструменту, я відтворю layout/copy один-в-один зі скріншоту, з покращеними іконками. Колірна палітра — Unity Editor (#383838 темно-сіра, #4C7EFF акцент), типографіка — Inter (стандарт editor UI).

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: write_file]_

_[tool: done]_

_[tool: fork_verifier_agent]_

Готово. Всі 8 екранів інсталера на одному полотні — можна перетягувати, відкривати на повний екран або вмикати focus на кожному:

1. **Welcome** — лого CAS, вибір методу установки (Git URL/UPM/Unity Package), поле URL
2. **Integration mode** — auto/manual картки з warning-блоком
3. **Components Overview** — таблиця SDK з кольоровими статусами, кнопками дій (Install/Update/Up to date) і Auto Init чекбоксами
4. **Hub Actions** — 4 тайли дій з blue-primary на головному
5. **Installation Progress** — список кроків з зеленими ✓, анімований spinner на активному, прогрес-бар 56%
6. **All Done** — велика зелена галочка, три CTA
7. **Update Installer** — версії, what's new, кнопки Later/Update Now
8. **CAS Settings Redirect** — переадресація з підказкою

Стиль Unity Editor dark: `#383838` вікна, акцент `#4C7EFF`, title-bar з лого CAS і X. Іконки намалював власноруч в SVG (робот, шестерня, чарівна паличка, dashboard, reset, big check). Логотипи SDK — простими гліфами як плейсхолдери, без точного копіювання брендів.

Якщо потрібно — можу перетворити в інтерактивний прототип з реальною навігацією Next/Cancel між екранами, або додати tweaks (наприклад, перемикач light/dark, ширина вікна, мова).

## User

Verifier agent check completed

## Assistant

Verifier помилка не пов'язана з кодом (502 від інфраструктури). Файл відкривається без console errors, екрани відмальовуються. Якщо щось виглядає не так — пишіть, виправлю.

