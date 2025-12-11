using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TaskManagerTelegramBot_borodin.Classes;
using System.Collections.Generic;

namespace TaskManagerTelegramBot_borodin
{
    public class Worker : IHostedService
    {
        private readonly TelegramBotClient bot;
        private readonly Database db = new();

        private readonly Dictionary<long, string> UserStates = new();
        private readonly Dictionary<long, object> UserTemp = new();

        private Timer? _timer;

        public Worker()
        {
            bot = new TelegramBotClient("8575293952:AAFmGdit8itXHevXyG1xTq0-WbXYiUXdExA");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Worker запущен.");

            var receiverOptions = new ReceiverOptions()
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            };

            bot.StartReceiving(
                HandleUpdateAsync,
                HandleErrorAsync,
                receiverOptions,
                cancellationToken
            );

            _timer = new Timer(Tick, null, TimeSpan.Zero, TimeSpan.FromSeconds(30));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Worker остановлен.");
            _timer?.Dispose();
            return Task.CompletedTask;
        }

        // ------------------------------------------------------------
        //   ОБРАБОТКА UPDATE
        // ------------------------------------------------------------
        private async Task HandleUpdateAsync(ITelegramBotClient _, Update update, CancellationToken token)
        {
            if (update.CallbackQuery != null)
            {
                await HandleCallback(update.CallbackQuery);
                return;
            }

            if (update.Message == null || update.Message.Type != MessageType.Text)
                return;

            long userId = update.Message.Chat.Id;
            string msg = update.Message.Text.Trim().ToLower();
            string username = update.Message.Chat.Username ?? "unknown";

            db.AddUser(userId, username);

            if (UserStates.ContainsKey(userId))
            {
                await ProcessStateMessage(userId, update.Message.Text);
                return;
            }

            switch (msg)
            {
                case "/start":
                    await SendMainMenu(userId);
                    break;

                case "добавить задачу":
                    UserStates[userId] = "choose_type";
                    await SendMessage(userId, "Выберите тип задачи:", CreateTaskTypeKeyboard());
                    break;

                case "посмотреть задачи":
                    await ShowTasks(userId);
                    break;

                case "удалить":
                    db.DeleteAllEvents(userId);
                    await SendMessage(userId, "Все задачи удалены.");
                    await SendMainMenu(userId);
                    break;

                default:
                    await SendMessage(userId, "Не понимаю. Используйте меню.");
                    break;
            }
        }

        // ------------------------------------------------------------
        //   INLINE кнопки — delete
        // ------------------------------------------------------------
        private async Task HandleCallback(CallbackQuery q)
        {
            if (q.Data.StartsWith("delete_"))
            {
                long id = long.Parse(q.Data.Replace("delete_", ""));
                db.DeleteEventById(id);

                await bot.AnswerCallbackQuery(q.Id);

                await bot.EditMessageText(
                    chatId: q.Message.Chat.Id,
                    messageId: q.Message.MessageId,
                    text: "❌ Задача удалена"
                );
            }
        }

        // ------------------------------------------------------------
        //   СОСТОЯНИЯ ДОБАВЛЕНИЯ ЗАДАЧ
        // ------------------------------------------------------------
        private async Task ProcessStateMessage(long userId, string msg)
        {
            string state = UserStates[userId];
            string low = msg.ToLower();

            switch (state)
            {
                case "choose_type":
                    if (low == "одноразовая")
                    {
                        UserStates[userId] = "one_time_date";
                        await SendMessage(userId, "Введите дату и время: 21:00 15.04.2025");
                    }
                    else if (low == "ежедневно")
                    {
                        UserStates[userId] = "daily_time";
                        await SendMessage(userId, "Введите время (HH:mm)");
                    }
                    else if (low == "еженедельно")
                    {
                        UserStates[userId] = "weekly_time";
                        await SendMessage(userId, "Введите время (HH:mm)");
                    }
                    break;

                case "one_time_date":
                    if (DateTime.TryParse(msg, out DateTime dt))
                    {
                        UserTemp[userId] = dt;
                        UserStates[userId] = "one_time_text";
                        await SendMessage(userId, "Введите текст задачи");
                    }
                    else
                    {
                        await SendMessage(userId, "Неверный формат. Пример: 21:00 15.04.2025");
                    }
                    break;

                case "one_time_text":
                    DateTime date = (DateTime)UserTemp[userId];
                    db.AddEvent(userId, msg, date, "none", null, null);

                    await SendMessage(userId, "Одноразовая задача добавлена!");
                    UserTemp.Remove(userId);
                    UserStates.Remove(userId);

                    await SendMainMenu(userId);
                    break;

                case "daily_time":
                    if (TimeSpan.TryParse(msg, out TimeSpan t))
                    {
                        UserTemp[userId] = t;
                        UserStates[userId] = "daily_text";
                        await SendMessage(userId, "Введите текст задачи");
                    }
                    else await SendMessage(userId, "Неверный формат времени.");
                    break;

                case "daily_text":
                    TimeSpan dtime = (TimeSpan)UserTemp[userId];
                    db.AddEvent(userId, msg, null, "daily", null, dtime);

                    await SendMessage(userId, "Ежедневная задача добавлена!");
                    UserTemp.Remove(userId);
                    UserStates.Remove(userId);

                    await SendMainMenu(userId);
                    break;

                case "weekly_time":
                    if (TimeSpan.TryParse(msg, out TimeSpan wtime))
                    {
                        UserTemp[userId] = wtime;
                        UserStates[userId] = "weekly_days";
                        await SendMessage(userId, "Введите дни недели (например: среда, пятница)");
                    }
                    else await SendMessage(userId, "Неверный формат времени.");
                    break;

                case "weekly_days":
                    var eng = RusToEngDays(low);
                    if (eng == null)
                    {
                        await SendMessage(userId, "Некорректные дни недели.");
                        return;
                    }

                    UserTemp[userId] = ((TimeSpan)UserTemp[userId], eng);
                    UserStates[userId] = "weekly_text";
                    await SendMessage(userId, "Введите текст задачи");
                    break;

                case "weekly_text":
                    var data = ((TimeSpan, string))UserTemp[userId];
                    db.AddEvent(userId, msg, null, "weekly", data.Item2, data.Item1);

                    await SendMessage(userId, "Еженедельная задача добавлена!");
                    UserTemp.Remove(userId);
                    UserStates.Remove(userId);

                    await SendMainMenu(userId);
                    break;
            }
        }

        // ------------------------------------------------------------
        // Кнопка удаления
        // ------------------------------------------------------------
        private InlineKeyboardMarkup DeleteEventButton(long id)
        {
            return new InlineKeyboardMarkup(
                InlineKeyboardButton.WithCallbackData("❌ Удалить", $"delete_{id}")
            );
        }

        private string? RusToEngDays(string input)
        {
            var map = new Dictionary<string, string>
            {
                {"понедельник","Monday"},
                {"вторник","Tuesday"},
                {"среда","Wednesday"},
                {"четверг","Thursday"},
                {"пятница","Friday"},
                {"суббота","Saturday"},
                {"воскресенье","Sunday"}
            };

            var result = new List<string>();

            foreach (var s in input.Split(',', StringSplitOptions.TrimEntries))
                if (map.ContainsKey(s)) result.Add(map[s]);

            return result.Count == 0 ? null : string.Join(",", result);
        }

        // ------------------------------------------------------------
        // ПОКАЗАТЬ ЗАДАЧИ
        // ------------------------------------------------------------
        private async Task ShowTasks(long userId)
        {
            var events = db.GetUserEvents(userId);

            if (events.Count == 0)
            {
                await SendMessage(userId, "У вас нет задач.");
                return;
            }

            foreach (var e in events)
            {
                string txt = $"• {e.Message}\n" +
                             $"Следующий запуск: {e.NextRun:HH:mm dd.MM.yyyy}\n" +
                             $"Тип: {e.RecurrenceType}";

                await bot.SendMessage(
                    chatId: userId,
                    text: txt,
                    replyMarkup: DeleteEventButton(e.Id)
                );
            }
        }
        private async void Tick(object? _)
        {
            var due = db.GetDueEvents();

            foreach (var e in due)
            {
                await SendMessage(e.UserId, $"🔔 Напоминание: {e.Message}");

                if (e.RecurrenceType == "none")
                    db.DeleteEventById(e.Id);
                else if (e.RecurrenceType == "daily")
                    db.UpdateNextRun(e.Id, e.NextRun.AddDays(1));
                else if (e.RecurrenceType == "weekly")
                    db.UpdateNextRun(e.Id, GetNextWeeklyRun(e));
            }
        }

        private DateTime GetNextWeeklyRun(Events e)
        {
            List<DayOfWeek> days = new();

            foreach (var x in e.WeeklyDays.Split(','))
                if (Enum.TryParse<DayOfWeek>(x, true, out var d))
                    days.Add(d);

            for (int i = 1; i <= 14; i++)
            {
                var check = e.NextRun.AddDays(i).Date + e.TimeOfDay!.Value;

                if (days.Contains(check.DayOfWeek))
                    return check;
            }

            return e.NextRun.AddDays(7);
        }
        private Task SendMessage(long chatId, string text, ReplyMarkup? kb = null)
        {
            return bot.SendMessage(
                chatId: chatId,
                text: text,
                replyMarkup: kb
            );
        }

        private ReplyKeyboardMarkup CreateTaskTypeKeyboard()
        {
            return new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Одноразовая" },
                new KeyboardButton[] { "Ежедневно" },
                new KeyboardButton[] { "Еженедельно" }
            })
            { ResizeKeyboard = true };
        }

        private Task SendMainMenu(long userId)
        {
            var kb = new ReplyKeyboardMarkup(new[]
            {
                new KeyboardButton[] { "Добавить задачу" },
                new KeyboardButton[] { "Посмотреть задачи" },
                new KeyboardButton[] { "Удалить" }
            })
            { ResizeKeyboard = true };

            return SendMessage(userId, "Главное меню:", kb);
        }

        private Task HandleErrorAsync(ITelegramBotClient _, Exception ex, CancellationToken token)
        {
            Console.WriteLine($"Ошибка Telegram API: {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
