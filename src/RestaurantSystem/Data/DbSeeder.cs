using RestaurantSystem.Models;
using RestaurantSystem.Services;

namespace RestaurantSystem.Data;

/// <summary>
/// Створює схему БД та наповнює її демонстраційними даними:
/// персонал, меню з технологічними картами, склад, план зали,
/// історія замовлень за 30 днів, активні замовлення та бронювання.
/// </summary>
public static class DbSeeder
{
    public static void EnsureCreatedAndSeeded(DbContextFactory factory, bool withDemoHistory = true)
    {
        using var db = factory.Create();
        db.Database.EnsureCreated();
        if (db.Employees.Any())
            return;

        Seed(db, withDemoHistory, DateTime.Now);
    }

    private static void Seed(RestaurantDbContext db, bool withDemoHistory, DateTime now)
    {
        // ---------- Персонал ----------
        var admin = Emp("Олена Ковальчук", "admin", "admin", UserRole.Administrator, "+380 67 120 45 10", 900);
        var waiter1 = Emp("Оксана Мельник", "waiter", "1111", UserRole.Waiter, "+380 50 311 22 47", 420);
        var waiter2 = Emp("Андрій Бондаренко", "andrii", "1111", UserRole.Waiter, "+380 93 774 10 05", 160);
        var cook1 = Emp("Тарас Шевчук", "cook", "2222", UserRole.Cook, "+380 66 908 31 12", 610);
        var cook2 = Emp("Марія Лисенко", "maria", "2222", UserRole.Cook, "+380 97 450 66 18", 75);
        db.Employees.AddRange(admin, waiter1, waiter2, cook1, cook2);

        // ---------- Склад ----------
        var ing = new Dictionary<string, Ingredient>();
        void I(string name, string unit, decimal qty, decimal min, decimal cost) =>
            ing[name] = new Ingredient { Name = name, Unit = unit, Quantity = qty, MinQuantity = min, CostPerUnit = cost };

        I("Картопля", "кг", 48, 15, 18);
        I("Буряк", "кг", 22, 8, 16);
        I("Капуста", "кг", 17, 6, 14);
        I("Цибуля", "кг", 14, 5, 15);
        I("Морква", "кг", 12, 5, 17);
        I("Помідори", "кг", 9.5m, 6, 85);
        I("Огірки", "кг", 7, 4, 70);
        I("Салат ромен", "кг", 2.1m, 3, 190);
        I("Гриби печериці", "кг", 6, 3, 120);
        I("Борошно", "кг", 35, 10, 22);
        I("Яйця", "шт", 160, 60, 5);
        I("Сир твердий", "кг", 6.2m, 3, 420);
        I("Моцарела", "кг", 4.8m, 3, 380);
        I("Бринза", "кг", 1.1m, 2, 340);
        I("Сметана", "кг", 8, 4, 95);
        I("Вершкове масло", "кг", 5.5m, 3, 360);
        I("Олія соняшникова", "л", 18, 6, 68);
        I("Куряче філе", "кг", 13, 6, 210);
        I("Яловичина", "кг", 9, 5, 420);
        I("Свинина", "кг", 11, 5, 260);
        I("Сало", "кг", 3.5m, 1.5m, 180);
        I("Сьомга", "кг", 0.9m, 2.5m, 890);
        I("Оселедець", "кг", 3, 1.5m, 160);
        I("Кукурудзяна крупа", "кг", 6, 2, 40);
        I("Сир кисломолочний", "кг", 7, 4, 140);
        I("Ягоди заморожені", "кг", 4.5m, 2, 230);
        I("Мед", "кг", 2.8m, 1, 260);
        I("Цукор", "кг", 20, 5, 32);
        I("Кава в зернах", "кг", 3.2m, 1.5m, 980);
        I("Молоко", "л", 14, 8, 42);
        I("Чай трав'яний", "кг", 0.35m, 0.4m, 1100);
        I("Лимони", "кг", 0.6m, 1.5m, 95);
        I("Сухофрукти", "кг", 3.5m, 1.5m, 160);
        I("Пепероні", "кг", 2.4m, 1, 450);
        db.Ingredients.AddRange(ing.Values);

        // ---------- Меню ----------
        var salads = Cat("Салати", "Leaf", "#7BC67E", 1);
        var soups = Cat("Перші страви", "PotSteam", "#E07A5F", 2);
        var mains = Cat("Гарячі страви", "FoodDrumstick", "#F2A65A", 3);
        var pizza = Cat("Піца", "Pizza", "#F4D35E", 4);
        var desserts = Cat("Десерти", "Cupcake", "#E58FB5", 5);
        var drinks = Cat("Напої", "GlassCocktail", "#6FB7E8", 6);
        db.Categories.AddRange(salads, soups, mains, pizza, desserts, drinks);

        var dishes = new List<Dish>();
        Dish D(Category c, string name, decimal price, string portion, int cook, string desc,
            params (string ingredient, decimal amount)[] recipe)
        {
            var d = new Dish
            {
                Category = c, Name = name, Price = price, Portion = portion,
                CookTimeMinutes = cook, Description = desc
            };
            foreach (var (iname, amount) in recipe)
                d.Ingredients.Add(new DishIngredient { Ingredient = ing[iname], Amount = amount });
            dishes.Add(d);
            return d;
        }

        D(salads, "Цезар з куркою", 245, "280 г", 12, "Ромен, куряче філе гриль, пармезан, крутони, соус цезар",
            ("Салат ромен", 0.12m), ("Куряче філе", 0.12m), ("Сир твердий", 0.02m), ("Яйця", 1));
        D(salads, "Грецький салат", 195, "260 г", 8, "Томати, огірки, бринза, оливки, червона цибуля",
            ("Помідори", 0.12m), ("Огірки", 0.1m), ("Бринза", 0.06m), ("Цибуля", 0.02m), ("Олія соняшникова", 0.02m));
        D(salads, "Вінегрет з оселедцем", 145, "250 г", 7, "Класичний вінегрет з печеним буряком та оселедцем",
            ("Буряк", 0.1m), ("Картопля", 0.06m), ("Морква", 0.04m), ("Капуста", 0.04m), ("Оселедець", 0.06m));
        D(salads, "Теплий салат з телятиною", 285, "270 г", 15, "Обсмажена телятина, мікс салатів, томати чері, гірчичний соус",
            ("Яловичина", 0.12m), ("Салат ромен", 0.08m), ("Помідори", 0.06m));

        D(soups, "Борщ український з пампушками", 165, "350/80 г", 10, "Наваристий борщ на кістковому бульйоні, сметана, часникові пампушки",
            ("Буряк", 0.08m), ("Капуста", 0.06m), ("Картопля", 0.06m), ("Свинина", 0.08m), ("Сметана", 0.03m), ("Борошно", 0.05m));
        D(soups, "Юшка з білої риби", 185, "350 г", 12, "Прозора рибна юшка з овочами та зеленню",
            ("Картопля", 0.06m), ("Морква", 0.03m), ("Цибуля", 0.02m));
        D(soups, "Курячий бульйон з локшиною", 125, "350 г", 8, "Домашня яєчна локшина, куряче м'ясо, зелень",
            ("Куряче філе", 0.07m), ("Борошно", 0.04m), ("Яйця", 1), ("Морква", 0.02m));
        D(soups, "Грибна юшка", 145, "350 г", 10, "Печериці, картопля, вершки та кріп",
            ("Гриби печериці", 0.1m), ("Картопля", 0.06m), ("Сметана", 0.03m));

        D(mains, "Вареники з картоплею та грибами", 155, "300 г", 12, "Ліплені вручну, зі смаженою цибулькою та сметаною",
            ("Борошно", 0.1m), ("Картопля", 0.12m), ("Гриби печериці", 0.04m), ("Цибуля", 0.03m), ("Сметана", 0.04m));
        D(mains, "Деруни зі сметаною", 145, "280 г", 14, "Хрусткі картопляні деруни, сметана, зелень",
            ("Картопля", 0.25m), ("Яйця", 1), ("Борошно", 0.02m), ("Сметана", 0.05m), ("Олія соняшникова", 0.04m));
        D(mains, "Котлета по-київськи", 265, "320 г", 20, "Куряче філе з вершковим маслом та зеленню, картопляне пюре",
            ("Куряче філе", 0.2m), ("Вершкове масло", 0.04m), ("Яйця", 1), ("Картопля", 0.15m), ("Борошно", 0.03m));
        D(mains, "Стейк Рібай", 690, "300 г", 25, "Мармурова яловичина на грилі, соус демі-гляс",
            ("Яловичина", 0.32m), ("Вершкове масло", 0.02m));
        D(mains, "Банош з бринзою та шкварками", 235, "320 г", 18, "Гуцульська кукурудзяна каша на сметані",
            ("Кукурудзяна крупа", 0.1m), ("Сметана", 0.1m), ("Бринза", 0.05m), ("Сало", 0.04m), ("Гриби печериці", 0.04m));
        D(mains, "Сьомга на грилі", 485, "250 г", 18, "Філе сьомги, спаржа, лимонно-вершковий соус",
            ("Сьомга", 0.18m), ("Лимони", 0.03m), ("Вершкове масло", 0.02m));
        D(mains, "Свинячі реберця BBQ", 345, "450 г", 22, "Томлені реберця в медово-гірчичній глазурі",
            ("Свинина", 0.4m), ("Мед", 0.03m), ("Картопля", 0.12m));

        D(pizza, "Маргарита", 215, "32 см", 15, "Томатний соус, моцарела, базилік",
            ("Борошно", 0.2m), ("Моцарела", 0.15m), ("Помідори", 0.08m), ("Олія соняшникова", 0.01m));
        D(pizza, "Пепероні", 255, "32 см", 15, "Гостра салямі пепероні, моцарела, томатний соус",
            ("Борошно", 0.2m), ("Моцарела", 0.14m), ("Пепероні", 0.08m), ("Помідори", 0.05m));
        D(pizza, "Чотири сири", 275, "32 см", 15, "Моцарела, твердий сир, бринза, вершковий соус",
            ("Борошно", 0.2m), ("Моцарела", 0.1m), ("Сир твердий", 0.06m), ("Бринза", 0.05m));

        D(desserts, "Сирники з ягідним соусом", 145, "220 г", 12, "Ніжні сирники зі сметаною та ягідним соусом",
            ("Сир кисломолочний", 0.18m), ("Яйця", 1), ("Борошно", 0.03m), ("Ягоди заморожені", 0.05m), ("Цукор", 0.02m));
        D(desserts, "Медовик", 120, "150 г", 3, "Класичний медовий торт зі сметанним кремом",
            ("Мед", 0.03m), ("Борошно", 0.05m), ("Сметана", 0.05m), ("Цукор", 0.03m), ("Яйця", 1));
        D(desserts, "Чізкейк Нью-Йорк", 135, "150 г", 3, "Вершковий чізкейк на пісочній основі",
            ("Сир кисломолочний", 0.1m), ("Вершкове масло", 0.03m), ("Цукор", 0.03m), ("Яйця", 1));

        D(drinks, "Узвар", 55, "300 мл", 1, "Домашній узвар із сухофруктів",
            ("Сухофрукти", 0.04m), ("Цукор", 0.01m));
        D(drinks, "Лимонад домашній", 85, "400 мл", 3, "Лимон, м'ята, мед",
            ("Лимони", 0.06m), ("Мед", 0.02m));
        D(drinks, "Американо", 55, "200 мл", 2, "Свіжообсмажена арабіка",
            ("Кава в зернах", 0.018m));
        D(drinks, "Капучино", 70, "250 мл", 3, "Подвійний еспресо та молочна піна",
            ("Кава в зернах", 0.018m), ("Молоко", 0.15m));
        D(drinks, "Чай трав'яний", 65, "500 мл", 3, "Карпатський збір: чебрець, м'ята, липа",
            ("Чай трав'яний", 0.008m), ("Мед", 0.01m));
        db.Dishes.AddRange(dishes);

        // ---------- План зали ----------
        var tables = new List<RestaurantTable>
        {
            T(1, 2, "Основна зала", TableShape.Round, 60, 75),
            T(2, 2, "Основна зала", TableShape.Round, 205, 75),
            T(3, 4, "Основна зала", TableShape.Square, 340, 65),
            T(4, 4, "Основна зала", TableShape.Square, 470, 65),
            T(5, 4, "Основна зала", TableShape.Square, 55, 250),
            T(6, 6, "Основна зала", TableShape.Long, 205, 255),
            T(7, 4, "Основна зала", TableShape.Square, 440, 250),
            T(8, 8, "Основна зала", TableShape.Long, 55, 440),
            T(9, 2, "Основна зала", TableShape.Round, 320, 445),
            T(10, 4, "Основна зала", TableShape.Square, 455, 435),
            T(11, 2, "Тераса", TableShape.Round, 650, 80),
            T(12, 4, "Тераса", TableShape.Round, 785, 70),
            T(13, 4, "Тераса", TableShape.Square, 712, 215),
            T(14, 8, "VIP", TableShape.Long, 662, 490),
        };
        db.Tables.AddRange(tables);
        db.SaveChanges();

        if (!withDemoHistory)
            return;

        // ---------- Історія замовлень за 3 місяці ----------
        var rnd = new Random(2024);
        var waiters = new[] { waiter1, waiter2 };
        var popular = dishes.SelectMany(d => Enumerable.Repeat(d, d.Category == drinks ? 3 : d.Price < 200 ? 2 : 1)).ToList();

        Order MakeOrder(DateTime created, RestaurantTable table, Employee waiter)
        {
            var order = new Order
            {
                Table = table,
                Waiter = waiter,
                CreatedAt = created,
                GuestsCount = rnd.Next(1, table.Seats + 1)
            };
            var positions = rnd.Next(2, 6);
            foreach (var dish in popular.OrderBy(_ => rnd.Next()).DistinctBy(d => d.Id).Take(positions))
            {
                order.Items.Add(new OrderItem
                {
                    Dish = dish,
                    Price = dish.Price,
                    Quantity = rnd.Next(10) < 7 ? 1 : 2,
                    Status = OrderItemStatus.Served,
                    SentAt = created.AddMinutes(3),
                    ReadyAt = created.AddMinutes(3 + dish.CookTimeMinutes)
                });
            }
            return order;
        }

        for (var day = 90; day >= 0; day--)
        {
            var date = now.Date.AddDays(-day);
            var weekend = date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday or DayOfWeek.Sunday;
            var count = rnd.Next(10, 17) + (weekend ? rnd.Next(5, 9) : 0);
            if (day == 0)
                count = 9;

            for (var n = 0; n < count; n++)
            {
                DateTime created;
                if (day == 0)
                {
                    // Сьогоднішні закриті замовлення — протягом останніх годин.
                    created = now.AddMinutes(-rnd.Next(70, 9 * 60));
                    if (created.Date != now.Date) continue;
                }
                else
                {
                    created = date.AddHours(11).AddMinutes(rnd.Next(0, 11 * 60));
                }

                var order = MakeOrder(created, tables[rnd.Next(tables.Count)], waiters[rnd.Next(waiters.Length)]);
                order.Status = OrderStatus.Paid;
                order.ClosedAt = created.AddMinutes(rnd.Next(35, 110));
                order.PaymentMethod = rnd.Next(10) < 6 ? PaymentMethod.Card : PaymentMethod.Cash;
                order.DiscountPercent = rnd.Next(10) == 0 ? 10 : 0;
                db.Orders.Add(order);
            }
        }

        // ---------- Активні замовлення (зараз у залі) ----------
        Dish Find(string name) => dishes.First(d => d.Name == name);

        OrderItem Item(string dish, int qty, OrderItemStatus st, int sentMinAgo, string note = "") => new()
        {
            Dish = Find(dish),
            Price = Find(dish).Price,
            Quantity = qty,
            Status = st,
            Note = note,
            SentAt = st == OrderItemStatus.Draft ? null : now.AddMinutes(-sentMinAgo),
            ReadyAt = st is OrderItemStatus.Ready or OrderItemStatus.Served ? now.AddMinutes(-2) : null
        };

        db.Orders.AddRange(
            new Order
            {
                Table = tables[2], Waiter = waiter1, GuestsCount = 3, CreatedAt = now.AddMinutes(-48),
                Items =
                {
                    Item("Борщ український з пампушками", 2, OrderItemStatus.Served, 44),
                    Item("Узвар", 3, OrderItemStatus.Served, 44),
                    Item("Котлета по-київськи", 1, OrderItemStatus.Ready, 30),
                    Item("Вареники з картоплею та грибами", 2, OrderItemStatus.Cooking, 22, "Без цибульки"),
                }
            },
            new Order
            {
                Table = tables[5], Waiter = waiter2, GuestsCount = 5, CreatedAt = now.AddMinutes(-26),
                Items =
                {
                    Item("Пепероні", 1, OrderItemStatus.Cooking, 18),
                    Item("Чотири сири", 1, OrderItemStatus.New, 6),
                    Item("Цезар з куркою", 2, OrderItemStatus.New, 6, "Соус окремо"),
                    Item("Лимонад домашній", 4, OrderItemStatus.Served, 20),
                    Item("Медовик", 2, OrderItemStatus.Draft, 0),
                }
            },
            new Order
            {
                Table = tables[8], Waiter = waiter1, GuestsCount = 2, CreatedAt = now.AddMinutes(-12),
                Items =
                {
                    Item("Стейк Рібай", 1, OrderItemStatus.New, 9, "Medium rare"),
                    Item("Грецький салат", 1, OrderItemStatus.Cooking, 9),
                    Item("Капучино", 2, OrderItemStatus.Ready, 9),
                }
            },
            new Order
            {
                Table = tables[11], Waiter = waiter2, GuestsCount = 4, CreatedAt = now.AddMinutes(-35),
                Items =
                {
                    Item("Банош з бринзою та шкварками", 2, OrderItemStatus.Cooking, 28),
                    Item("Сирники з ягідним соусом", 2, OrderItemStatus.New, 3),
                    Item("Чай трав'яний", 2, OrderItemStatus.Served, 30),
                }
            });

        // ---------- Бронювання ----------
        var slot = new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0).AddHours(2);
        db.Reservations.AddRange(
            new Reservation { Table = tables[13], GuestName = "Ірина Савчук", Phone = "+380 67 555 12 34", GuestsCount = 7, StartsAt = slot, Note = "День народження, потрібен торт" },
            new Reservation { Table = tables[6], GuestName = "Дмитро Гончар", Phone = "+380 50 123 98 76", GuestsCount = 4, StartsAt = slot.AddMinutes(30) },
            new Reservation { Table = tables[0], GuestName = "Софія Ткаченко", Phone = "+380 63 404 40 40", GuestsCount = 2, StartsAt = slot.AddHours(1), Note = "Біля вікна" },
            new Reservation { Table = tables[7], GuestName = "ТОВ «Агроінвест»", Phone = "+380 44 220 11 00", GuestsCount = 8, StartsAt = now.Date.AddDays(1).AddHours(19), Note = "Бізнес-вечеря" },
            new Reservation { Table = tables[11], GuestName = "Василь Петренко", Phone = "+380 98 765 43 21", GuestsCount = 3, StartsAt = now.Date.AddDays(2).AddHours(13).AddMinutes(30) },
            new Reservation { Table = tables[3], GuestName = "Наталія Кравець", Phone = "+380 99 010 20 30", GuestsCount = 4, StartsAt = now.Date.AddDays(1).AddHours(20), Status = ReservationStatus.Cancelled });

        db.SaveChanges();
    }

    private static Employee Emp(string name, string login, string password, UserRole role, string phone, int daysAgo) => new()
    {
        FullName = name,
        Login = login,
        PasswordHash = PasswordHasher.Hash(password),
        Role = role,
        Phone = phone,
        HiredAt = DateTime.Today.AddDays(-daysAgo)
    };

    private static Category Cat(string name, string icon, string color, int order) =>
        new() { Name = name, Icon = icon, Color = color, SortOrder = order };

    private static RestaurantTable T(int number, int seats, string zone, TableShape shape, double x, double y) =>
        new() { Number = number, Seats = seats, Zone = zone, Shape = shape, X = x, Y = y };
}
