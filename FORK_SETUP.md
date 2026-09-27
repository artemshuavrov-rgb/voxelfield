# Работа с форком Voxelfield

Этот репозиторий — форк [qhdwight/voxelfield](https://github.com/qhdwight/voxelfield).

## Запуск проекта

1. Клонируйте репозиторий вместе с подмодулем:

   ```sh
   git clone --recurse-submodules https://github.com/artemshuavrov-rgb/voxelfield.git
   ```

   Если репозиторий уже клонирован, выполните `git submodule update --init --recursive`.
2. Установите **Unity 6000.3.15f1** — версия записана в `ProjectSettings/ProjectVersion.txt`.
3. Откройте папку репозитория как существующий проект в Unity Hub.
4. Для сборки используйте меню **Build → [тип и архитектура]**, как указано в исходном README.

## Репозитории Git

- `origin` — этот форк;
- `upstream` — исходный проект.

Чтобы добавить `upstream` в новой локальной копии:

```sh
git remote add upstream https://github.com/qhdwight/voxelfield.git
```

## Лицензии

Код основного проекта опубликован с файлом `LICENSE.md` (GPL-3.0). Перед выпуском собственной игры проверьте лицензии всех моделей, звуков, шрифтов, пакетов Unity и других сторонних материалов. Подмодуль LiteNetLib содержит собственный файл `LICENSE.txt`.
