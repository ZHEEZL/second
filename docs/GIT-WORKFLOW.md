# Git: как работать командой

Репозиторий: **https://github.com/ZHEEZL/second**. Ветка `main` уже создана и
запушена, в ней первый коммит, `.gitignore`, CI и шаблон PR. Каждый участник
клонирует репозиторий, работает в своей ветке и открывает PR.

```text
main ──●──────────●──────────●────►
        \        /  \        /
         ●──●──●     ●──●──●
       feature/sudoku   feature/closest-pair
```

## 1. Кто вы в git

Проверьте:

```powershell
git config user.name
git config user.email
```

Если пусто или неверно:

```powershell
git config --global user.name "Имя Фамилия"
git config --global user.email "you@example.com"
```

Имя и почта попадут в коммиты — преподаватель увидит авторство.

> **Важно:** чтобы GitHub показывал вас в **Contributors**, email из коммита
> должен быть привязан к GitHub-аккаунту. Проверить можно так:
> `git log -1 --format='%an <%ae>'`. Если GitHub показывает 0 контрибьюторов,
> добавьте этот email в **GitHub → Settings → Emails** (и подтвердите) или
> используйте noreply-адрес вида
> `12345678+username@users.noreply.github.com` (цифры — ваш GitHub ID).

## 2. Клонировать репозиторий

```powershell
git clone https://github.com/ZHEEZL/second.git
cd second
```

Дальше все команды — из корня проекта (там, где `RecursiveLab.sln`).

## 3. Проверить remote

```powershell
git remote -v
```

Ожидается:

```text
origin  https://github.com/ZHEEZL/second.git (fetch)
origin  https://github.com/ZHEEZL/second.git (push)
```

Если `origin` нет (например, вы создали проект заново), добавьте:

```powershell
git remote add origin https://github.com/ZHEEZL/second.git
git push -u origin main
```

## 4. Ветка на задачу

Каждый участник делает свою ветку от свежего `main`:

```powershell
git switch main
git pull --ff-only
git switch -c feature/sudoku
```

Имена веток:

| Задача | Ветка |
|--------|-------|
| H-дерево | `feature/htree` |
| Судоку | `feature/sudoku` |
| Ближайшая пара | `feature/closest-pair` |
| Ханойские башни | `feature/hanoi` |
| Инфраструктура/доки | `chore/…` |

Первый push:

```powershell
git push -u origin feature/sudoku
```

## 5. Коммиты

Формат: `тип(область): что сделано`.

```text
feat(sudoku): рекурсивный перебор с возвратом
feat(htree): добавить анимацию построения
fix(ui): не перекрывать подписи стержней
docs(hanoi): дополнить чек-лист приёмки
chore(ci): кэшировать NuGet-пакеты
```

Правила:

- один коммит — одна законченная мысль;
- не коммитить `bin/`, `obj/`, `.nuget/`, `artifacts/` — они уже в `.gitignore`;
- перед коммитом: `git status` и `git diff --staged`.

## 6. Pull Request

1. Запушить ветку.
2. GitHub предложит **Compare & pull request** — открыть PR в `main`.
3. Заполнить шаблон PR (он появится автоматически).
4. Дождаться зелёного CI (`build-and-test`).
5. Попросить review у другого участника.
6. После approve — **Squash and merge**, ветку удалить.

В PR должно быть понятно: какую задачу закрывает, как проверить, что изменено.

## 7. Синхронизация с main

Пока вы работали, `main` мог уйти вперёд:

```powershell
git fetch origin
git switch main
git pull --ff-only
git switch feature/sudoku
git merge main
```

Если конфликт — решить, запустить `dotnet build` и `dotnet test`,
затем `git add` и `git commit`.

Общие файлы (`LabTaskCatalog.cs`, `MainWindow.axaml`, `docs/TEAM-PLAN.md`)
правятся редко и только по договорённости; лучше отдельный PR на регистрацию
новой задачи.

## 8. Защита main

В GitHub: **Settings → Branches → Add branch protection rule** для `main`:

- [x] Require a pull request before merging
- [x] Require approvals (1)
- [x] Require status checks to pass: `build-and-test`
- [x] Do not allow bypassing the above settings (по желанию)

Тогда случайный push в `main` не сломает общую сборку.

## 9. CI

`.github/workflows/ci.yml` запускается на каждый push и PR в `main`:

1. ставит .NET 8;
2. `dotnet restore`;
3. `dotnet build -c Release` (без предупреждений);
4. `dotnet test` (контрактные тесты с `Skip` не мешают).

Локально те же шаги:

```powershell
dotnet build -c Release
dotnet test
```

## 10. Шпаргалка

```powershell
git status                      # что изменено
git switch -c feature/…         # новая ветка
git add <файлы>                 # подготовить
git commit -m "feat(…): …"      # коммит
git push -u origin feature/…    # первый push
git log --oneline --graph       # история
git fetch origin                # подтянуть remote
git switch main && git pull     # обновить main
git merge main                  # влить main в свою ветку
```

Если что-то пошло не так: `git status` подскажет команды; не делайте
`git push --force` в общие ветки.
