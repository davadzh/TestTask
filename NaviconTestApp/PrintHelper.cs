using NLog;
using System;

namespace NaviconTestApp
{
    public static class PrintHelper
    {
        // Логгирование
        private static Logger logger = LogManager.GetCurrentClassLogger();

        public static void Greetings()
        {
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine("Добро пожаловать в веб-сервис для загрузки картинок с сайтов");
            Console.WriteLine("------------------------------------------------------------");
            Console.WriteLine();
            logger.Info("User started the service");
        }

        public static void PromptToEnterUrl()
        {
            Console.WriteLine("- Чтобы скачать фото с сайта, введите его URL");
            Console.Write(">>> ");
        }

        public static void PromptToEnterThreadsCount()
        {
            Console.WriteLine("- Введите желаемое количество потоков (не менее 1)");
            Console.Write(">>> ");
        }

        public static void PromptToEnterImageCount()
        {
            Console.WriteLine("- Введите желаемое количество фотографий (не менее 1)");
            Console.Write(">>> ");
        }

        public static void PromptToUseServiceAgain()
        {
            Console.WriteLine("- Введите q для выхода, либо что угодно другое, чтобы продолжить работу с сервисом");
            Console.Write(">>> ");
        }

        public static void Goodbye()
        {
            Console.WriteLine("--------------------------------");
            Console.WriteLine("Завершение работы с веб-сервисом");
            Console.WriteLine("--------------------------------");
            logger.Info("User stoped the service");
        }

        public static void InvalidInput()
        {
            Console.WriteLine("Данные введены некорректно");
            logger.Warn("User entered incorrect data");
        }

        public static void InvalidValue()
        {
            Console.WriteLine("Значение должно быть не меньше 1");
            logger.Warn("User entered value less than 1");
        }

        public static void InvalidUrlFormat()
        {
            Console.WriteLine("Неверный формат Url");
            logger.Warn("User entered incorrect url");
        }
    }
}
