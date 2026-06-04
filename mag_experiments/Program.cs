Console.WriteLine("mag_experiments");
string db_path = @"C:\Home\data\getstarted\";
var files = Directory.GetFiles(db_path);
foreach (var file in files) File.Delete(file);

mag_experiments.Experiment1.Run();
//mag_experiments.Exp2.Run();
//mag_experiments.Exp3EmptyElement.Run();
//mag_experiments.Exp4SequenceBase.Run();
//mag_experiments.Exp5EKeyIndex.Run();
//mag_experiments.Exp6Like.Run();
//mag_experiments.Exp7Indexes.Run();
