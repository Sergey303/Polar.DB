// Переименовываем не работающие в работающие модули и создаем тестовые прогораммы
// USequenceBase берем из экспериментов
// USequence берем из экспериментов переименовав UniversalSequence,
// UKeyIndex берем из экспериментов переименовав UniversalKeyIndex,

Console.WriteLine("mag_tests");
string dbPath = $"C:\\Home\\data\\getstarted\\";
if (!Directory.Exists(dbPath))
    Directory.CreateDirectory(dbPath);
var files = Directory.GetFiles(dbPath);
foreach (var file in files) File.Delete(file);


// Сделаем и поместим в рабочую директорию USequenceBase
//mag_tests.Test1.Run(dbPath); // Проверка USequenceBase в позиции последовательности
// 5 млн. записей. Загрузка 723 + 237 мс., выборки по ключу 43 мс / 10 тыс

// Поместим в рабочую директорию UKeyIndex и USequence. Проверяется KV-хранилище при простом режиме работы
mag_tests.Exp2KeyValueStorage.Run(dbPath); // Key-value storage. Проверка USequence
// 5 млн. записей. Загрузка 2182 мс., выборки по ключу 81 мс / 10 тыс
// 5 млн. записей, src-вариант. Загрузка 1901 мс. выборка по ключу  94 / 10 тыс. 

// Поработаем с внешним ключевым индексом EKeyIndex
//mag_series.Exp3Indexes.Run(dbPath);

// Поработаем с тремя внешними ключевыми индексами
//mag_series.Exp7.Run(dbPath);

