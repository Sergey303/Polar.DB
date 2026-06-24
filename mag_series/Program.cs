// Переименовываем не работающие в работающие модули и создаем тестовые прогораммы
// USequenceBase берем из экспериментов
// USequence берем из экспериментов переименовав UniversalSequence,
// UKeyIndex берем из экспериментов переименовав UniversalKeyIndex,

Console.WriteLine("mag_series");
string dbPath = $"C:\\Home\\data\\getstarted\\";
if (!Directory.Exists(dbPath))
    Directory.CreateDirectory(dbPath);
var files = Directory.GetFiles(dbPath);
foreach (var file in files) File.Delete(file);

// Сделаем и поместим в рабочую директорию USequenceBase
//mag_series.Experiment1.Run(dbPath); // Проверка USequenceBase в позиции последовательности

// Поместим в рабочую директорию UKeyIndex и USequence
//mag_series.Exp2KeyValueStorage.Run(dbPath); // Key-value storage. Проверка USequence

// Поработаем с внешним ключевым индексом EKeyIndex
mag_series.Exp3Indexes.Run(dbPath);

////mag_experiments.Exp4SequenceBase.Run();
////mag_experiments.Exp5EKeyIndex.Run();
////mag_experiments.Exp6Like.Run();
// mag_experiments.Exp7Indexes.Run();
//mag_experiments.Exp7.Run();
////mag_experiments.Experiment2.Run();
////mag_experiments.Experiment3.Run();
//mag_experiments.PersonExternalKeySample.Run(dbPath);