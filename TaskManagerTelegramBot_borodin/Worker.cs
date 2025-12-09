using System.Reflection.Metadata.Ecma335;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot_borodin
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        readonly string Token = "";
        TelegramBotClient TelegramBotClient;
        List<User> User = new List<User>();
        Timer Timer;
        List<string> message = new List<string>()
        {
            "Здравствуйте!"
            +$"\nРады приветствовать вас в Telegram-боте @Напоминатор@!"
            +"\nНаш бот создан для того, чтобы напоминать вам о важных событиях и мероприятиях. С ним вы точно не пропустите ничего важного!"
            +"\nНе забудьте добавить бота в список своих контактов и настроить уведомления. Тогда вы всегда будете в курсе событий!",
            "Укажите дату и время напоминания в следующем формате: "
            +"\n<i><b>21:51 26.04.2025</b>"
            +"\n Напомни о том что я хотел сходить в магазин.</i>",
            "Кажется что-то не получилось."
            +"\n<i><b>21:51 26.04.2025</b>"
            +"\n Напомни о том что я хотел сходить в магазин.</i>",
            "",
            "Задачи пользователя не найдены.",
            "Событие удалено.",
            "Все события удалены."
        };

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_logger.IsEnabled(LogLevel.Information))
                {
                    _logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(1000, stoppingToken);
            }
        }

        public bool CheckFormatDateTime(string value, out DateTime time)
        {
            return DateTime.TryParse(value, out time);
        }
        private static ReplyKeyboardMarkup GetButtons()
        {
            List<KeyboardButton> keyboardButtons = new List<KeyboardButton>();
            keyboardButtons.Add(new KeyboardButton("Удалить все задачи"));

            return new ReplyKeyboardMarkup
            {
                Keyboard = new List<List<KeyboardButton>>
                {
                    keyboardButtons
                }
            };
        }
        /// <summary>
        /// Создание кнопки для удаление конкретного собыития
        /// </summary>
        /// <param name="Message">Сообщение</param>
        /// <returns></returns>
        public static InlineKeyboardMarkup DeleteEvent(string Message)
        {
            List<InlineKeyboardButton> inlineKeyboardButtons = new List<InlineKeyboardButton>();
            inlineKeyboardButtons.Add(new InlineKeyboardButton("Удалить", Message));
            return new InlineKeyboardMarkup(inlineKeyboardButtons);
        }
    }
}
