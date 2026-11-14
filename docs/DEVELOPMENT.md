# Разработка

## Назначение

«Сканер QR» — Android-приложение для терминала сбора данных **АТОЛ Smart.Lite** (Android 7.0, armeabi-v7a) на **.NET 10 MAUI**. Функциональность соответствует приложению QRScanner; оформление строгое, по мотивам Scan2ERP. Язык интерфейса — русский.

## Требования

- .NET 10 SDK;
- workload `android` (и MAUI, если сборка выполняется через `dotnet`);
- Android SDK 36+;
- JDK 17+;
- эмулятор или ТСД с Android 7+ для проверки.

## Сборка и тесты

```powershell
dotnet restore QRScanner.Maui.slnx
dotnet test tests/QRScanner.Core.Tests/QRScanner.Core.Tests.csproj
dotnet build src/QRScanner.App/QRScanner.App.csproj -c Debug -f net10.0-android
```

## Dev-сборка

Отдельный package id позволяет установить приложение рядом со старой версией:

```powershell
dotnet build src/QRScanner.App/QRScanner.App.csproj `
  -c Release -f net10.0-android `
  -p:ApplicationId=com.companyname.qrscanner.dev `
  -p:ApplicationTitle="Сканер QR Dev" `
  -p:EmbedAssembliesIntoApk=true
```

APK:

```text
src/QRScanner.App/bin/Release/net10.0-android/com.companyname.qrscanner.dev-Signed.apk
```

Установка:

```powershell
adb install -r <путь-к-APK>
```

## Структура

```text
src/QRScanner.App/       — MAUI Android: интерфейс, DI, SQLite, сканер, API, QR
src/QRScanner.Core/      — парсинг штрихкодов, миграция, константы
tests/QRScanner.Core.Tests/
```

## Архитектура

- Shell и DI: `AppShell`, `MauiProgram`.
- Данные: SQLite + SQLCipher, ключ в `SecureStorage`.
- Сканер: broadcast `com.xcheng.scanner.action.BARCODE_DECODING_BROADCAST`.
- Datamark: `/auth`, `/items/findItems`.
- QR: генерация PNG, системное «Поделиться», сохранение через SAF.
- Заказы: сохранение в БД, экспорт `.txt`.

## Ключевые файлы

- `Data/DatabaseService.cs` — подключение и CRUD;
- `Data/SecureStorageDbKeyProvider.cs` — ключ БД;
- `Services/ScannerService.cs` — приём сканера;
- `Services/DatamarkApiClient.cs` — Datamark;
- `Services/QrCodeService.cs` — QR-код короба;
- `Services/FileSaveService.cs` — сохранение файла через системный выбор;
- `ViewModels/CorobDetailViewModel.cs` — короб, марки, QR;
- `ViewModels/OrderViewModel.cs` — заказ и экспорт;
- `ViewModels/SettingsViewModel.cs` — настройки Datamark;
- `Resources/Styles/Styles.xaml` — дизайн-система.

## Ветвление

- `main` — стабильная ветка, всегда собирается.
- Новые функции — `feature/...`, исправления — `fix/...`.
- Ветка `develop` не используется.
- Изменения принимаются в `main` после проверки и подтверждения.

## Требования к интерфейсу

- Только русский язык.
- Палитра: `#1E3A8A`, `#F4F5F7`, белый, тёмно-серый текст.
- Клавиши: `1` — основное действие, `2` — QR, `0` — назад.
- PIN и роли отсутствуют: настройки доступны любому пользователю ТСД.
- Звук — штатный сканер.
- Справка соответствует текущему интерфейсу.

## Бизнес-правила

- Ёмкость короба: 12, 10, 5 или 3 марки.
- Одна марка не может находиться в двух коробах.
- Смешанный заказ разрешён: короба и отдельные марки.
- Без авторизации Datamark марка не добавляется: выводится сообщение и кнопка перехода в настройки.
- При удалении заказа короба остаются на складе, статус «Отгружен» снимается.
- В заказе доступны действия «Отправлено» и «Вернуть на склад».
- История заказов сохраняется.
- Данные удаляются только вручную.
- Поиск по марке: марка → короб → удаление марки.

## Безопасность

- Пароль Datamark и ключ БД хранятся в `SecureStorage`.
- Локальная база шифруется SQLCipher.
- Логи не содержат паролей и ключей.
- APK, базы, `credentials.txt`, `.env`, keystore и `dist/` исключены из Git.

## Проверка перед коммитом

```powershell
dotnet test tests/QRScanner.Core.Tests/QRScanner.Core.Tests.csproj
dotnet build src/QRScanner.App/QRScanner.App.csproj -c Debug -f net10.0-android
```

Для изменений интерфейса и сценариев сканирования дополнительно требуется проверка на эмуляторе или ТСД.
