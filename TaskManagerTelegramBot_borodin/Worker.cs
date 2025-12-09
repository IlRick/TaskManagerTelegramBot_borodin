using System.Reflection.Metadata.Ecma335;
using TaskManagerTelegramBot_borodin.Classes;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TaskManagerTelegramBot_borodin
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        readonly string Token = "";
        TelegramBotClient TelegramBotClient;
        List<Users> User = new List<Users>();
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
        /// <summary>
        /// Метод отправки сообщения
        /// </summary>
        /// <param name="chatId">код пользователя</param>
        /// <param name="typeMessag">Команда</param>
        public async void SendMessage(long chatId, int typeMessag)
        {
            if(typeMessag!=3)
            {
                await TelegramBotClient.SendMessage(chatId, message[typeMessag], ParseMode.Html, replyMarkup: GetButtons());
            }
            else if(typeMessag==3)
                await TelegramBotClient.SendMessage(chatId, $"Указанный вами время и даты не могут быть устанволены, потому-что сечас уже: {DateTime.Now.ToString("HH.mm dd.MM.yyyy")}");
            
        }


        public async void Command(long chatId, string command)
        {
            if (command.ToLower() == "/start") SendMessage(chatId, 0);
            else if (command.ToLower() == "/create_task") SendMessage(chatId, 1);
            else if (command.ToLower() == "/list_task")
            {
                Users user= User.Find(x=>x.IdUser == chatId);
                if (user == null) SendMessage(chatId, 4);
                else if(user.Events.Count==0) SendMessage(chatId, 4);
                else
                {
                    foreach(Events Event in user.Events)
                    {
                        await TelegramBotClient.SendMessage(chatId,
                            $"Уведомить пользователя: {Event.Time.ToString("HH:mm dd:MM:yyyy")}" +
                            $"\nСообщение: {Event.Message}",
                            replyMarkup: DeleteEvent(Event.Message));
                    }
                }
            }
        }

        private void GetMessages(Message message)
        {
            Console.WriteLine("Получено сообщение:"+message.Text+ "от пользователя: "+ message.Chat.Username);
            long IdUser=message.Chat.Id;
            string MessageUser= message.Text;
            if(message.Text.Contains("/"))
            {
                Command(message.Chat.Id, message.Text);
            }
            else if(message.Text.Equals("Удалить все задачи"))
            {
                Users user = User.Find(x => x.IdUser == message.Chat.Id);
                if (user == null) SendMessage(message.Chat.Id, 4);
                else if (user.Events.Count == 0) SendMessage(user.IdUser, 4);
                else
                {
                    user.Events= new List<Events>();
                    SendMessage(user.IdUser, 6);
                }
            }
            else
            {
                Users user = User.Find(x => x.IdUser == message.Chat.Id);
                if(user==null)
                {
                    user= new Users(message.Chat.Id);
                    User.Add(user);
                }
                string[] Info= message.Text.Split('\n');
                if(Info.Length <2)
                {
                    SendMessage(message.Chat.Id, 2);
                    return;
                }
                DateTime Time;
                if (CheckFormatDateTime(Info[0],out Time)==false)
                {
                    SendMessage(message.Chat.Id, 2);
                    return;
                }
                user.Events.Add(new Events(Time, message.Text.Replace(Time.ToString("HH:mm dd.MM.yyyy") + "\n", "")));
            }
                
        }
    }
}
