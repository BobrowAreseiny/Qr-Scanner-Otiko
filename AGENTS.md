# AGENTS.md — память проекта

Главный файл для нового чата или нового агента. Прочитай его и `docs/PROJECT-STATE.md` до любых изменений.

## Проект

«Сканер QR» — Android-приложение для ТСД **АТОЛ Smart.Lite** (Android 7.0, armeabi-v7a) на **.NET 10 MAUI**.
Функционал повторяет старый QRScanner, оформление строгое, по мотивам Scan2ERP. Интерфейс только на русском.

- Рабочий репозиторий: `C:\Users\Bobro\source\repos\QRScanner-Maui`
- GitHub: https://github.com/BobrowAreseiny/Qr-Scanner-Otiko
- Старый Xamarin-репозиторий — только архив, не изменять: `C:\Users\Bobro\source\repos\QR-Scanner-Xamarin`

## Быстрый старт

```powershell
cd C:\Users\Bobro\source\repos\QRScanner-Maui
git status
git branch -a
git log --oneline --decorate --all -20
```

Затем прочитать `docs/PROJECT-STATE.md` и `README.md`.

## Сборка и проверка

```powershell
dotnet test tests/QRScanner.Core.Tests/QRScanner.Core.Tests.csproj
dotnet build src/QRScanner.App/QRScanner.App.csproj -c Debug -f net10.0-android
```

Dev-APK:

```powershell
dotnet build src/QRScanner.App/QRScanner.App.csproj `
  -c Release -f net10.0-android `
  -p:ApplicationId=com.companyname.qrscanner.dev `
  -p:ApplicationTitle="Сканер QR Dev" `
  -p:EmbedAssembliesIntoApk=true
```

APK: `src/QRScanner.App/bin/Release/net10.0-android/com.companyname.qrscanner.dev-Signed.apk`

Эмулятор: `adb -s emulator-5554 ...`. Реальный ТСД: ATOL Smart.Lite, Android 7.0.

## Ветки

- `main` — стабильная, всегда собирается.
- Фичи — `feature/...`, фиксы — `fix/...`.
- `develop` нет: работаем вдвоём.
- Мердж в `main` — только после подтверждения пользователя.
- Незамердженные ветки не считать частью `main`.

## Жёсткие правила

- Никаких секретов в Git, логах, README и сообщениях: пароли Datamark, ключ БД, keystore.
- Не коммитить APK, `.db`, `credentials.txt`, `.env`, `dist/`, keystore.
- Пользовательские данные удаляются только вручную, без автоочистки.
- UI только русский. Палитра: `#1E3A8A`, `#F4F5F7`, белый, тёмно-серый текст.
- Клавиши: `1` — основное действие, `2` — дополнительное (QR), `0` — назад.
- Одна марка не может быть в двух коробах.
- Без авторизации Datamark марку не добавлять: сообщение + кнопка перехода в настройки.
- Смешанный заказ разрешён: короба и отдельные марки.
- Удаление заказа: короба остаются на складе, статус «Отгружен» снимается.
- В заказе два действия: «Отправлено» и «Вернуть на склад».
- Ёмкость короба: 12 / 10 / 5 / 3.
- Поиск по марке нужен: марка → короб → удаление марки.

## Архитектура

- `src/QRScanner.App/` — MAUI Android: Shell, DI (`MauiProgram.cs`), `Views`, `ViewModels`, `Data`, `Services`.
- `src/QRScanner.Core/` — парсинг ШК, миграция, константы.
- `tests/QRScanner.Core.Tests/` — тесты Core.

Ключевые файлы:

- `Data/DatabaseService.cs` — SQLCipher-подключение и CRUD;
- `Data/SecureStorageDbKeyProvider.cs` — ключ БД в SecureStorage;
- `Services/ScannerService.cs` — приём broadcast сканера;
- `Services/DatamarkApiClient.cs` — `/auth`, `/items/findItems`;
- `Services/QrCodeService.cs` — QR короба (ветка `feature/qr-share`);
- `ViewModels/CorobDetailViewModel.cs` — сканирование марок, Datamark, QR;
- `ViewModels/OrderViewModel.cs` — заказ и экспорт (ветка `feature/shipment-order-export`);
- `ViewModels/SettingsViewModel.cs` — настройки Datamark;
- `Resources/Styles/Styles.xaml` — дизайн-система.

## Текущее состояние

Кратко: `main` — каркас, агрегация, SQLCipher, Datamark-клиент.
Готовые, но ещё не слитые ветки: `feature/shipment-order-export`, `feature/qr-share`.
В работе: `feature/db-recovery`.

Подробности, решения и бэклог — в `docs/PROJECT-STATE.md`.
