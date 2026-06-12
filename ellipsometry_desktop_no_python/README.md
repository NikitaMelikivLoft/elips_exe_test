# Эллиптический метод расчёта d — десктопная программа без Python

Это Windows Forms приложение на C#/.NET. Python не нужен.

## Что делает

- Ввод Ψexp, Δexp, λ, θ₀.
- Выбор материалов из базы или ручной ввод n,k.
- Расчёт β, q, rs, rp, ρ, Ψ, Δ, F(d).
- Поиск толщины d по минимуму F(d).
- Сохранение CSV в папку `outputs`.

## Где сохраняются CSV

После нажатия кнопки `Сохранить CSV в outputs` файл сохраняется в папку:

```text
outputs
```

Если запускается `.exe`, папка `outputs` создаётся рядом с `.exe`.

## Как получить EXE без установки Python

### Вариант 1: GitHub Actions

1. Создай репозиторий на GitHub.
2. Загрузи туда все файлы из этого архива.
3. Открой вкладку `Actions`.
4. Запусти workflow `Build Windows EXE`.
5. После сборки скачай artifact `EllipsometrySolver-win-x64`.
6. Внутри будет `EllipsometrySolver.exe` и папка `outputs`.

### Вариант 2: собрать локально без Python

Нужен только `.NET SDK 8`.

1. Установи .NET SDK 8:
   https://dotnet.microsoft.com/download
2. Запусти:

```text
build_windows_exe.bat
```

Готовый exe появится здесь:

```text
bin/Release/net8.0-windows/win-x64/publish/EllipsometrySolver.exe
```

## Важно

Собранный exe публикуется как self-contained, то есть на компьютере пользователя не нужен Python и обычно не нужен установленный .NET runtime.
