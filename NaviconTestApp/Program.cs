using NLog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace NaviconTestApp
{
    class Program
    {
        // Логгирование
        private static Logger logger = LogManager.GetCurrentClassLogger();

        // Массив потоков здесь для того, чтобы отслеживать завершенность всех дочерних потоков
        static List<Thread> threads = new List<Thread>();

        // Список объектов Host представляет из себя будущий JSON
        static List<Host> hosts = new List<Host>();

        // Объект фото с его параметрами (как в JSON)
        static List<Image> images = new List<Image>();

        // Семафор для управления синхронизацией потоков
        static Semaphore sem;

        // Всего файлов, которые будем пытаться скачать
        static int allFilesCount = 0;

        // Счетчик скаченных файлов 
        static int downloadedFilesCount = 0;


        static void Main(string[] args)
        {
            PrintHelper.Greetings();

            // Создаем директорию для сохранения картинок, если ее не существует
            Directory.CreateDirectory(Config.SavePath);

            // Цикл ввода данных
            while (true)
            {
                // Получение данных от пользователя
                var inputData = InputData();

                // Передача данных пользователя в основной метод
                GetImages(inputData.Url, inputData.ThreadsCount, inputData.ImageCount).GetAwaiter();

                // Продолжаем работу в Main, если все дочерние потоки завершили свою работу
                foreach (Thread thread in threads)
                {
                    if (thread.IsAlive) thread.Join();
                }

                logger.Info("All files have been downloaded");
                // Обнуляем счетчик файлов и общее количество файлов
                downloadedFilesCount = 0;
                allFilesCount = 0;

                // Копируем список изображений, дабы избежать привязки потери данных по ссылке при чистке
                Image[] imgs = new Image[images.Count];
                images.CopyTo(imgs);

                // Добавляем данные о загруженных фото в объект Host
                hosts.Add(new Host
                {
                    host = inputData.Url,
                    images = imgs
                });

                // Очищаем List с фото
                images.Clear();

                // Предлагаем пользователю либо продолжить работу с сервисом,
                // либо завершить ее для получения данных в виде JSON
                PrintHelper.PromptToUseServiceAgain();
                string answer = Console.ReadLine();
                if (answer.ToLowerInvariant().Equals("q"))
                    break;
            }

            PrintHelper.Goodbye();

            // Сериализуем объект в JSON
            string json = JsonSerializer.Serialize(hosts, new JsonSerializerOptions() { 
                WriteIndented = true // Форматируем для удобного и читаемого отображения
            });

            // На выходе получаем результат в виде JSON
            Console.WriteLine(json);
            
            Console.ReadLine();
        }


        // Основной метод
        public static async Task GetImages(string url, int threadCount, int imageCount)
        {
            // Получаем HTML в виде строки
            HttpClient client = new HttpClient();
            var htmlString = await client.GetAsync(url).GetAwaiter()
                                                     .GetResult()
                                                     .Content
                                                     .ReadAsStringAsync();
            logger.Info("HTML on requested URL was loaded");

            // Адский ад! Cделать регулярку и для src, и для alt одновременно,
            // увы, не смог. С HtmlAgilityPack это заняло бы пару минут

            // Создаем регулярку, которая забирает src из каждого тега img
            string pattern = "<img.+?src=[\"'](.+?)[\"'].*?>";
            Regex rgx = new Regex(pattern, RegexOptions.IgnoreCase);
            MatchCollection matches = rgx.Matches(htmlString);

            // Задаем семафору инициализационное количество потоков и максимально возможное
            sem = new Semaphore(threadCount, threadCount);

            if (matches.Count < imageCount)
                allFilesCount = matches.Count;
            else
                allFilesCount = imageCount;
            Console.WriteLine($"Всего найдено {allFilesCount} файлов");


            // Если фотографий меньше, чем imageCount, то качаем все, которые нашли на странице
            for (int i = 0, l = matches.Count; i < l && i < imageCount ; i++)
            {
                var image = new Image {
                    src = matches[i].Groups[1].Value,
                    alt = string.Empty, // Пустой, поскольку не получилось достать через регулярку
                };

                // Создаем поток, передаем в него метод класса Image для скачивания фото, затем запускаем поток
                var childThread = new Thread(image.LoadImageAndSave);
                threads.Add(childThread);
                childThread.Start();
            }
        }


        class Host
        {
            // Нейминг с маленькой буквы исходя из ТЗ
            public string host { get; set; }
            public Image[] images { get; set; }
        }


        class Image
        {
            // Нейминг с маленькой буквы исходя из ТЗ
            public string src { get; set; }
            public string alt { get; set; }
            public string size { get; set; }

            // Метод скачивания и сохранения файлов
            public void LoadImageAndSave()
            {
                sem.WaitOne();

                try
                {
                    WebClient client = new WebClient();

                    // Проверяем ссылку на валидность
                    Uri.TryCreate(src, UriKind.Absolute, out Uri uri);
                    
                    // Если не получилось создать ссылку, либо если путь не имеет расширения - пропускаем
                    if (uri != null)
                    {
                        if (Path.HasExtension(uri.AbsoluteUri))
                        {
                            var fileName = uri.Segments.LastOrDefault();

                            // Получаем размер файла
                            client.OpenRead(uri);
                            size = Convert.ToInt64(client.ResponseHeaders["Content-Length"]).ToString();

                            // Скачиваем файл
                            client.DownloadFile(uri, $"{Config.SavePath}\\{Guid.NewGuid()}_{fileName}");
                            Console.WriteLine($"Скачано {++downloadedFilesCount}/{allFilesCount} файлов");
                            logger.Info("File downloaded successfully");

                            // Добавляем его в общий список файлов
                            images.Add(this);
                        }
                        else
                        {
                            logger.Warn($"No extension in URI {uri}");
                        }
                    }
                    else
                    {
                        logger.Warn($"Failed to create URI from {uri}");
                    }
                }
                catch (Exception e) {
                    logger.Error($"Unexpected error while loading file. Error message: {e.Message}");
                }
                
                sem.Release();
            }


        }


        // Фунция ввода данных от пользователя с проверкой на корректность вводимых данных
        private static InputDataModel InputData()
        {
            bool isSuccessParsed;

            while (true)
            {
                PrintHelper.PromptToEnterUrl();
                string currentUrl = Console.ReadLine();
                if (!Uri.TryCreate(currentUrl, UriKind.Absolute, out Uri uri))
                {
                    PrintHelper.InvalidUrlFormat();
                    continue;
                }

                PrintHelper.PromptToEnterThreadsCount();
                isSuccessParsed = int.TryParse(Console.ReadLine(), out int currentThreadsCount);
                if (!isSuccessParsed)
                {
                    PrintHelper.InvalidInput();
                    continue;
                }
                else if (currentThreadsCount < 1)
                {
                    PrintHelper.InvalidValue();
                    continue;
                }

                PrintHelper.PromptToEnterImageCount();
                isSuccessParsed = int.TryParse(Console.ReadLine(), out int currentImageCount);
                if (!isSuccessParsed)
                {
                    PrintHelper.InvalidInput();
                    continue;
                }
                else if (currentThreadsCount < 1)
                {
                    PrintHelper.InvalidValue();
                    continue;
                }

                return new InputDataModel
                {
                    Url = currentUrl,
                    ThreadsCount = currentImageCount,
                    ImageCount = currentImageCount
                };
            }
        }
    }
}
