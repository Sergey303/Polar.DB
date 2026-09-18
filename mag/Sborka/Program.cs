using Sborka;

int npersons = 5_000_000;
bool toload = true;
if (args.Length == 2) { npersons = int.Parse(args[0]); toload = (args[1] == "true"); }
    
Console.WriteLine("Старт моей базы данных");

Random rnd = new Random();
System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();

string dbPath = $"C:\\Home\\data\\getstarted\\";
if (!Directory.Exists(dbPath)) Directory.CreateDirectory(dbPath);

if (toload) foreach (var file in Directory.GetFiles(dbPath)) File.Delete(file);

// =========== Стандартный тест на key-value: загрузка и выборки
//Sborka.mag_test1.Run(dbPath, npersons, toload);
// Результаты на 5 млн.:  загрузка 1254 мс. выборка 80 мс. / 10 тыс. запрсов. Подключение 222 мс., запросы 88 мс.

// =========== тест на подмену значений
//Sborka.mag_test2.Run(dbPath, npersons, toload);

// =========== тест на загрузку через добавление
//Sborka.mag_test3.Run(dbPath, npersons, toload);
// Результаты на 5 млн.:  загрузка 1696 мс. выборка 4 мс. / 10 тыс. запрсов. Подключение 1770 мс., запросы 4 мс.

// =========== тест на пустые элементы
//Sborka.mag_test4.Run(dbPath, npersons, toload);

// =========== тест на сканирование
Sborka.mag_test5.Run(dbPath, npersons, toload);
// загрузка 5 млн. в базовую последовательность 660 мс.
