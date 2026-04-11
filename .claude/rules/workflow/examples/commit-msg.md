# Ví dụ commit message đúng/sai

## Convention
`{type}({module}): {imperative verb} {what}`

- Tất cả lowercase
- Không dấu chấm cuối
- Types: `feat`, `fix`, `refactor`, `test`, `chore`, `docs`, `style`
- Modules: `identity`, `safety`, `checkins`, `notification`, `storage`, `settings`, `shared`

---

## Đúng

```
feat(identity): add RegisterUser command handler with email validation
feat(checkins): implement CreateCheckin endpoint with media upload
fix(identity): correct JWT expiry calculation using UTC instead of local time
fix(safety): prevent duplicate daily safety record per user per day
refactor(shared): extract PaginatedList CreateAsync to extension method
test(identity): add unit tests for LoginCommandHandler happy and sad paths
chore(migration): add Checkin and CheckinMedia tables
chore(migration): add soft-delete IsDeleted column to UserDevice
docs(readme): update architecture diagram with Shared layer
```

## Sai (và lý do)

```
fixed stuff          ← không rõ fix cái gì
update               ← quá chung chung
WIP                  ← không nên commit WIP
add code             ← vô nghĩa
feat: add things to system   ← không có module, không rõ "things" là gì
Fixed authentication bug.    ← viết hoa, có dấu chấm, không có module
Added new endpoint    ← dùng past tense thay vì imperative verb
```

---

## Commit nhiều file liên quan

Dùng body để giải thích thêm khi cần:
```
feat(identity): add LoginCommand with JWT generation

- LoginCommandHandler validates credentials against bcrypt hash
- Issues 15-minute access token + 7-day refresh token
- Records LastLoginAtUtc on User entity
```
