// Испытываем более экономный вариант USequenceBase вместо UniversalSequenceBase, другой вариант UniversalSequence
// вместо USequence, другой вариант UniversalKeyIndex вместо UKeyIndex.

Console.WriteLine("mag_experiments");
string db_path = @"C:\Home\data\getstarted\";
var files = Directory.GetFiles(db_path);
foreach (var file in files) File.Delete(file);

//mag_experiments.Experiment1.Run(); // Проверка USequenceBase
//mag_experiments.Exp2KeyValueStorage.Run(); // Key-value storage. Проверка UniversalSequence
////mag_experiments.Exp4SequenceBase.Run();
////mag_experiments.Exp5EKeyIndex.Run();
////mag_experiments.Exp6Like.Run();
mag_experiments.Exp7Indexes.Run();
//mag_experiments.Exp7.Run();
////mag_experiments.Experiment2.Run();
////mag_experiments.Experiment3.Run();