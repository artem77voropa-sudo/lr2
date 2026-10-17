# Контракт ЛР2, baseline 2-A, рівень «Добре»

| Поле POST | Перевірка | Помилка |
|---|---|---|
| title | Обов'язкове після Trim; вхідна довжина до Trim ≤160 | 400, title |
| description | Обов'язкове після Trim; вхідна довжина до Trim ≤4000 | 400, description |
| severity | TryParse без урахування регістру та IsDefined | 400, severity |
| occurredAtUtc | Обов'язковий DateTimeOffset; не пізніше за зафіксований now+5 хв; запис у UTC | 400, occurredAtUtc |
| High/Critical + description | Щонайменше 40 символів після Trim | 400, description |
| title + стан БД | Збіг нормалізованого title з урахуванням регістру в New/Triaged/InProgress/Resolved | 409; Closed не блокує |

Сервер призначає id, ownerUserId=Alice, status=New, createdAtUtc та updatedAtUtc.
Request DTO має лише чотири клієнтські поля; зайві JSON-поля ігноруються.
201 повертає CreatedIncidentResponse і Location. Entity напряму не серіалізується.
400/404/409 мають application/problem+json, title/status; 400 містить errors, 404/409 — detail.

Пошук: буквальний підрядок у Title/Description без урахування регістру; null q означає порожній підрядок.
План виправлення: передавати q параметром через LINQ/ILike, екранувати \\, %, _ для буквального пошуку;
sortBy обирає лише готову гілку LINQ: createdAtUtc DESC (також default), severity за рангом Critical→Low,
status за рангом New→Closed. Другий ключ Id ASC; після сортування Take(50). Невідомий sortBy — 400.

Обмеження: AnyAsync до запису не усуває гонку одночасних POST; новий індекс або migration не входить до ЛР2.
