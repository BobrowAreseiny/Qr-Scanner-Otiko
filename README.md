# Сканер QR

Новое Android-приложение для терминала сбора данных **АТОЛ Smart.Lite** на .NET 10 MAUI.
Функционал повторяет QRScanner, оформление — строгое, по мотивам Scan2ERP.

Текущее состояние, решения и план — [docs/PROJECT-STATE.md](docs/PROJECT-STATE.md).
Сборка, архитектура и правила разработки — [docs/DEVELOPMENT.md](docs/DEVELOPMENT.md).

## Реализованный функционал

- главное меню, «Агрегация», «Отгрузка», «Настройки»;
- управление пунктами меню с физической клавиатуры: **1 / 2 / 0**;
- создание короба с ёмкостью **12 / 10 / 5 / 3**;
- список коробов;
- экран короба: название, количество, марки;
- приём сканера через broadcast `com.xcheng.scanner`;
- проверка дублей и ограничение ёмкости;
- сохранение коробов и марок;
- SQLCipher для локальной базы;
- настройки Datamark: сервер, логин, пароль в SecureStorage;
- клиент Datamark `/auth` и `/items/findItems`;
- иконка и splash со сканером;
- сборка под `armeabi-v7a` (ATOL Smart.Lite), `arm64-v8a`, `x86_64`.

## Требования

- .NET 10 SDK;
- workload `android` (и MAUI, если собирается через `dotnet`);
- Android SDK 36+;
- JDK 17+.

## Сборка

```powershell
dotnet restore QRScanner.Maui.slnx
dotnet test tests/QRScanner.Core.Tests/QRScanner.Core.Tests.csproj
dotnet build src/QRScanner.App/QRScanner.App.csproj -c Debug -f net10.0-android
```

Dev-сборка с отдельным package id, чтобы ставить рядом со старым QRScanner:

```powershell
dotnet build src/QRScanner.App/QRScanner.App.csproj `
  -c Release -f net10.0-android `
  -p:ApplicationId=com.companyname.qrscanner.dev `
  -p:ApplicationTitle="Сканер QR Dev" `
  -p:EmbedAssembliesIntoApk=true
```

APK появится в:

```
src/QRScanner.App/bin/Release/net10.0-android/com.companyname.qrscanner.dev-Signed.apk
```

Установка:

```powershell
adb install -r <путь-к-apk>
```

## Настройка сканера на ТСД

В приложении **Barcode Utility** (или **Barcode Service**):

1. `Scan Setting` → `Data Receive Method` → `Broadcast_Event`.
2. Включить `Left ScanKey`, `Right ScanKey`, `Front ScanKey`.
3. `Module Setting` → включить нужные типы ШК, в том числе **Data Matrix**.

Для `Barcode Service` проверить:

- `IntentName`: `com.xcheng.scanner.action.BARCODE_DECODING_BROADCAST`
- `ExtraData`: `EXTRA_BARCODE_DECODING_DATA`
- `ExtraSymbology`: `EXTRA_BARCODE_DECODING_SYMBOL`

Приложение при старте отправляет `com.xcheng.scanner.action.CONTROL_DATA_EVENT`
с `extra_data_event = BROADCAST_EVENT`, чтобы включить broadcast-режим.

## Структура

```
src/QRScanner.App/     — MAUI Android: UI, DI, SQLite, сканер, API
src/QRScanner.Core/    — парсинг ШК, миграция, константы
tests/QRScanner.Core.Tests/
```

## Безопасность

- пароль Datamark и ключ БД хранятся в `SecureStorage`;
- локальная база шифруется SQLCipher;
- APK, базы, `credentials.txt`, `.env` и keystore исключены из Git;
- вход по PIN отсутствует по решению заказчика;
- резервный код ключа БД не реализован.

## Статус

Готово в отдельных ветках, но ещё не слито в `main`:

- отгрузка и экспорт `.txt` — `feature/shipment-order-export`;
- QR-код короба и «Поделиться» — `feature/qr-share`;
- восстановление ключа БД — `feature/db-recovery` (в работе).

Не реализовано:

- история заказов и действия «Отправлено» / «Вернуть на склад»;
- запрет добавления марки без авторизации Datamark;
- сценарий «Информация о коробе» (поиск марки по коробам);
- резервные копии БД и экран кода восстановления;
- файловые логи;
- справка;
- экспорт для 1С;
- CI (GitHub Actions);
- проверка с реальными учётными данными Datamark;
- финальная release-подпись.

Подробный план — в [docs/PROJECT-STATE.md](docs/PROJECT-STATE.md).
