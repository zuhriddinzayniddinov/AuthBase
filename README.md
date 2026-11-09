# Core — Shared Base Microservices Library (.NET 10)

`Core` — barcha mikroservislar uchun **100% mustaqil, servis-neytral (hech qanday servisga yoki ma'lumotlar bazasi jadvallariga qaram bo'lmagan)** umumiy infratuzilma kutubxonasi.

Loyiha mikroservislarning o'zaro bog'liqligini (Coupling) yo'qotish, korporativ xavfsizlik, tranzaksiyalar ishonchliligi va yuqori tezlikni ta'minlash uchun mo'ljallangan.

---

## 📌 Mundarija

1. [Boshqa Repozitoriyga Qo'shish va Ulash (Git Subtree)](#-1-boshqa-repozitoriyga-qoshish-va-ulash-git-subtree)
2. [Tezkor Boshlash (Program.cs)](#-2-tezkor-boshlash-programcs)
3. [Barcha Imkoniyatlar (Fitchalar), Ulash va Plus / Minuslari](#-3-barcha-imkoniyatlar-fitchalar-ulash-va-plus--minuslari)
   - [1. Taqsimlangan Ruxsatlar va Sinxronizatsiya (Decentralized Permissions)](#1-taqsimlangan-ruxsatlar-va-sinxronizatsiya-decentralized-permissions)
   - [2. Taqsimlangan Qulflash (Redis Distributed Lock + Watchdog)](#2-taqsimlangan-qulflash-redis-distributed-lock--watchdog)
   - [3. .NET 10 HybridCache (L1 In-Memory + L2 Redis)](#3-net-10-hybridcache-l1-in-memory--l2-redis)
   - [4. Fintech Idempotensiya Middleware (X-Idempotency-Key)](#4-fintech-idempotensiya-middleware-x-idempotency-key)
   - [5. Sessiya va Tokenni Bekor Qilish (Session Revocation Middleware)](#5-sessiya-va-tokenni-bekor-qilish-session-revocation-middleware)
   - [6. PostgreSQL & EF Core 10 Asoslari (CoreDbContext)](#6-postgresql--ef-core-10-asoslari-coredbcontext)
   - [7. Ko'p Ijarachilik (Multi-Tenancy Izolyatsiyasi)](#7-kop-ijarachilik-multi-tenancy-izolyatsiyasi)
   - [8. MassTransit + Transactional Outbox (RabbitMQ)](#8-masstransit--transactional-outbox-rabbitmq)
   - [9. Code-First gRPC (protobuf-net.Grpc)](#9-code-first-grpc-protobuf-netgrpc)
   - [10. Xavfsizlik Sarlavhalari, Rate Limiting va Siqish](#10-xavfsizlik-sarlavhalari-rate-limiting-va-siqish)

---

## 📥 1. Boshqa Repozitoriyga Qo'shish va Ulash (Git Subtree)

`Core` repozitoriyasini mikroservislarga ulash uchun **Git Subtree** usuli tanlangan.

### 💡 Nima uchun Git Submodule emas, Git Subtree?
- **Yangi kelgan dasturchi uchun 100% shaffof:** Yangi dasturchi hech qanday maxsus buyruq yoki qoidalarni bilishi shart emas. U shunchaki odatdagidek `git clone` qiladi va servisni ishga tushirib ishlayveradi (`Core` oddiy ichki papka sifatida loyiha bilan birga keladi).
- **Submodule boshog'rig'i yo'q:** `detached HEAD`, submoduleni push qilishni unutish, `git pull` da submodule papkasi bo'sh qolib ketishi kabi muammolar mutlaqo bo'lmaydi.
- **Mustaqil CI/CD:** Serverda yoki Docker da build bo'lganda ham qo'shimcha submodule kalitlari yoki sozlamalar talab qilinmaydi.

---

### 1-qadam. Mikroservisga Core ni birinchi marta qo'shish (Setup)

Mikroservis repozitoriyasi ildizida (root papkada) bitta buyruq bilan `Core` ni qo'shib olasiz:

```bash
# Core ni `src/Core` papkasiga bitta commit bilan qo'shish:
git subtree add --prefix src/Core <Core-git-repo-url> main --squash
```
> `--squash` kaliti `Core` ning yuzlab eski commitlarini bitta toza commit qilib qo'shadi, natijada mikroservis git tarixi toza bo'lib qoladi.

Qulaylik uchun mikroservisda `Core` ga remote alias qo'shib qo'yish mumkin:
```bash
git remote add core-origin <Core-git-repo-url>
```

---

### 2-qadam. Yangi kelgan dasturchi uchun ish boshlash (Developer Onboarding)

Yangi dasturchi shunchaki mikroservisni klon qiladi va ishlayveradi:
```bash
git clone <Mikroservis-repo-url>
cd <Mikroservis-papka>
dotnet run
```
Dasturchi uchun `Core` oddiy ichki papka bo'lib ko'rinadi. U `git pull`, `git push` qilganda faqat o'z servisining repozitoriyasi bilan ishlaydi.

---

### 3-qadam. Core'dagi yangilanishlarni tortib olish (Pull Updates)

Agar `Core` kutubxonasiga yangi funksiyalar yoki o'zgarishlar kiritilsa, uni mikroservisga tortib olish:

```bash
git subtree pull --prefix src/Core core-origin main --squash
# yoki to'g'ridan-to'g'ri URL orqali:
git subtree pull --prefix src/Core <Core-git-repo-url> main --squash
```

---

### 4-qadam. Mikroservis loyihalariga (.csproj) Reference berish

Mikroservisning qatlamlariga qarab quyidagicha Project Reference beriladi:

```xml
<!-- Mikroservis Domain qatlamiga: -->
<ItemGroup>
  <ProjectReference Include="..\Core\src\Core.Domain\Core.Domain.csproj" />
</ItemGroup>

<!-- Mikroservis Infrastructure qatlamiga: -->
<ItemGroup>
  <ProjectReference Include="..\Core\src\Core.Infrastructure\Core.Infrastructure.csproj" />
</ItemGroup>

<!-- Mikroservis Web / Api qatlamiga: -->
<ItemGroup>
  <ProjectReference Include="..\Core\src\Core.Web\Core.Web.csproj" />
</ItemGroup>
```

---

### 5-qadam (Ixtiyoriy). Mikroservis ichidan Core'ga tuzatish yuborish (Push Back)

Agar mikroservisda ishlab turib `Core` ichidagi biror xatolikni to'g'rilasangiz va uni markaziy `Core` repozitoriyasiga ham qaytarib yubormoqchi bo'lsangiz:
```bash
git subtree push --prefix src/Core core-origin main
```

---

## ⚡ 2. Tezkor Boshlash (`Program.cs`)

Barcha standart xizmatlar va middleware'larni bitta qator bilan ishga tushirish mumkin:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Barcha standartlarni (DbContext, Logging, HybridCache, DistributedLock, RateLimiter, MassTransit) yoqish:
builder.ConfigureDefault<MyDbContext>();

var app = builder.Build();

// Pipeline (SecurityHeaders, GlobalException, CorrelationId, Idempotency, SessionValidation):
await app.ConfigureDefault<MyDbContext>();

app.MapControllers();
app.Run();
```

---

## 🛠️ 3. Barcha Imkoniyatlar (Fitchalar), Ulash va Plus / Minuslari

---

### 1. Taqsimlangan Ruxsatlar va Sinxronizatsiya (Decentralized Permissions)

`Core` kutubxonasida hech qanday servisga xos monolit enum yoki ma'lumotlar bazasi jadvallari yo'q. Har bir servis o'zining ruxsatlariga o'zi egalik qiladi.

#### A) Oddiy Mikroservisda (masalan: `QuranCourse`, `OrderService`):

1. **O'zining ichida ruxsatlar Enum'ini yaratadi:**
```csharp
using System.ComponentModel;
using Core.Domain.Permissions;

[PermissionsModule("QuranCourse", serviceCode: 60, moduleName: "Courses")]
public enum QuranPermissions
{
    [Description("Yangi kurs yaratish")]
    CreateCourse = 60100001,

    [Description("Kurslarni ko'rish")]
    ViewCourses  = 60100002
}
```

2. **Kontrollerda ruxsatni tekshirish:**
```csharp
[HttpPost]
[PermissionAuthorize(QuranPermissions.CreateCourse)]
public async Task<IActionResult> CreateCourse(...) { ... }
```

3. **`Program.cs` da avtomatik yuborishni yoqish:**
```csharp
// Servis start bo'lishi bilan ruxsatlar ro'yxati RabbitMQ orqali markaziy servisga yuboriladi:
builder.Services.AddCorePermissionAutoSync<QuranPermissions>();
```

#### B) Ruxsatlarni qabul qiluvchi / saqlovchi xizmatda (masalan: `AuthApi` yoki `PermissionService`):

`Core` ma'lumotlar bazasi bilan to'g'ridan-to'g'ri ishlamaydi. Qabul qiluvchi servis o'z bazasiga o'zi saqlashi uchun `IPermissionStorageHandler` interfeysini implement qiladi:

1. **Storage Handlerni yozish:**
```csharp
public class MyPermissionStorageHandler(MyDbContext db) : IPermissionStorageHandler
{
    public async Task<int> HandlePermissionsReceivedAsync(
        string serviceName, 
        int? serviceCode, 
        IReadOnlyList<PermissionDefinition> permissions, 
        CancellationToken ct)
    {
        // Servis o'zining ma'lumotlar bazasi jadvaliga permissions ro'yxatini (Code, Name, Description...)
        // xohlagan usulda (Upsert, EF Core, Dapper) saqlaydi yoki yangilaydi.
        return permissions.Count;
    }
}
```

2. **`Program.cs` da ro'yxatdan o'tkazish:**
```csharp
// 1. Qabul qiluvchini ulash (agar o'zining lokal enumi bo'lsa, startda uni ham to'g'ridan-to'g'ri saqlaydi):
builder.Services.AddCorePermissionReceiver<MyPermissionStorageHandler, AuthPermissions>();

// 2. MassTransit busiga consumerni qo'shish:
builder.Services.AddCoreMassTransit<MyDbContext>(builder.Configuration, bus =>
{
    bus.AddPermissionSyncConsumer(); // Boshqa servislardan kelgan ruxsatlarni ushlaydi
});
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:**
  - `Core` mutlaqo toza va database-agnostic.
  - Servislar o'rtasida qaramlik (tight coupling) yo'q. Yangi endpoint qo'shilsa `Core` ni yangilash shart emas.
  - Ruxsatlar nomlari va tavsiflari inson tushunadigan tilda markaziy bazada doim yangilanib turadi.
- **⚠️ Minus:**
  - Servis kodi prefikslari (masalan: `60`, `40`, `10`) to'qnashib ketmasligi uchun jamoada raqamlash tartibiga kelishib olinishi kerak.

---

### 2. Taqsimlangan Qulflash (Redis Distributed Lock + Watchdog)

Parallel serverlarda yoki mikroservis nusxalarida bitta resursni (masalan: to'lov yoki buyurtma) bir vaqtda o'zgartirib qo'yishning (Race condition) oldini oladi.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. Program.cs da ulash:
builder.Services.AddCoreDistributedLock(builder.Configuration.GetConnectionString("Redis"));

// 2. Servis ichida ishlatish:
public class PaymentService(IDistributedLock distributedLock)
{
    public async Task ProcessPaymentAsync(long orderId, CancellationToken ct)
    {
        await using var lockHandle = await distributedLock.TryAcquireLockAsync(
            resourceKey: $"order:payment:{orderId}",
            timeout: TimeSpan.FromSeconds(5),
            expiry: TimeSpan.FromSeconds(30),
            ct);

        if (lockHandle is null)
            throw new ConflictException("Ushbu buyurtma ayni vaqtda boshqa jarayonda bajarilmoqda!");

        // Qulf olindi! Uzoq operatsiya davomida Watchdog fonida qulf muddatini avtomatik uzaytiradi.
        // Blok tugashi bilan (using) qulf xavfsiz bo'shatiladi.
    }
}
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Parallel dublikat to'lov yoki operatsiyalarni 100% yo'qotadi. Fonda `PeriodicTimer` orqali Watchdog bor (qulf operatsiya tugamasdan eskirib qolmaydi).
- **⚠️ Minus:** Redis serveri ishlab turishi shart. Tarmoq sekinlashsa timeout xatosi qaytarishi mumkin.

---

### 3. .NET 10 `HybridCache` (L1 In-Memory + L2 Redis)

L1 xotira (nanosoniyalarda) va L2 Redis keshini yagona xizmatda birlashtiradi. **Lock-on-Read (Single-Flight)** orqali Cache Stampede muammosini bartaraf etadi.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. Program.cs da ulash:
builder.Services.AddCoreHybridCache(builder.Configuration.GetConnectionString("Redis"));

// 2. Servis ichida ishlatish:
public class ProductService(IHybridCacheService cache, MyDbContext db)
{
    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync(
            key: $"product:{id}",
            factory: async token => await db.Products.FindAsync([id], token),
            localTtl: TimeSpan.FromSeconds(30),  // L1 In-Memory (juda tez)
            remoteTtl: TimeSpan.FromMinutes(10), // L2 Redis (taqsimlangan)
            tags: ["products"],
            cancellationToken: ct);
    }

    public async Task UpdateAsync(ProductDto dto, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        // Barcha mahsulotlar keshini teg bo'yicha bir zumda tozalash:
        await cache.RemoveByTagAsync("products", ct);
    }
}
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Cache Stampede bo'lmaydi (bir vaqtda 10,000 so'rov kelsa ham bazaga faqat 1 tasi boradi). L1 xotira tufayli javob berish tezligi 10-100 baravar oshadi.
- **⚠️ Minus:** L1 xotirasi server RAM'idan foydalanadi (juda katta gigabaytlik obyektlarni keshlamaslik lozim).

---

### 4. Fintech Idempotensiya Middleware (`X-Idempotency-Key`)

Mijoz yoki tarmoq so'rovni takror yuborganida dublikat tranzaksiyalar yaratilishini to'xtatadi. So'rov tanasining `SHA-256` xeshini tekshiradi.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// Program.cs da:
builder.Services.AddCoreIdempotency();
app.UseCoreIdempotency(); // UseAuthentication dan keyin

// Mijoz yuboradigan so'rov sarlavhasi (Header):
// POST /api/v1/orders
// X-Idempotency-Key: e4b29b68-6c84-48f1-8f55-23fba93b79da
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Foydalanuvchi "To'lash" tugmasini 2 marta bossa ham mablag' faqat 1 marta yechiladi. Bir xil kalit bilan boshqa ma'lumot yuborilsa `409 Conflict` qaytaradi.
- **⚠️ Minus:** Mijoz tomoni har bir yangi operatsiya uchun alohida unikal UUID yuborishi shart.

---

### 5. Sessiya va Tokenni Bekor Qilish (Session Revocation Middleware)

Foydalanuvchi "Logout" qilganda yoki paroli o'zgarganda uning mavjud JWT tokenini darhol bekor qilish imkoniyati.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// Program.cs da:
builder.Services.AddCoreSessionValidation();
app.UseCoreSessionValidation(); // UseAuthentication dan keyin qo'yiladi
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Stateless JWT ning eng katta muammosini (muddati tugaguncha tokenni o'chirib bo'lmaslik) hal qiladi. Redis yuklamasini kamaytirish uchun L1 xotirada 20 soniya keshlanadi.
- **⚠️ Minus:** Har bir HTTP so'rovda tekshiriladi (L1 kesh tufayli nanosoniyalarda ishlasa-da, bekor qilingan token maksimal 20 soniya ichida to'liq kuchini yo'qotadi).

---

### 6. PostgreSQL & EF Core 10 Asoslari (`CoreDbContext`)

Mikroservis ma'lumotlar bazasi bilan ishlashini optimallashtiruvchi tayyor bazaviy DbContext.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. DbContext voris oladi:
public class OrderDbContext : CoreDbContext
{
    public OrderDbContext(DbContextOptions<OrderDbContext> options, IServiceProvider sp, ITenantContext tc)
        : base(options, sp, tc) { }

    public DbSet<Order> Orders => Set<Order>();
}

// 2. Dinamik filtrlash (Parametrlashtirilgan Dynamic LINQ):
var query = db.Orders.AsQueryable();
var result = await query.FilterByExpressions(metaQueryModel).ToReadOnlyListAsync(ct);
```

#### Xususiyatlari:
- **`xmin` Optimistik qulflash:** Entitiylarga qo'shimcha `RowVersion` ustunisiz, PostgreSQL tizim ustuni `xmin` orqali parallel o'zgartirishlarni nazorat qiladi.
- **Parametrlashtirilgan Dynamic LINQ:** Dynamic so'rovlar SQL parametrlari (`@p0`) shaklida yuboriladi va EF Core `CompiledQueryCache` ni 100% qayta ishlatadi.
- **`ToReadOnlyListAsync`:** ChangeTracker xarajatlarisiz ma'lumotlarni tezkor o'qish.

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** O'ta yuqori SQL unumdorligi, SQL Injection xavfi nol, avtomatik `CreatedAt`/`UpdatedAt` va `IsDelete` boshqaruvi.
- **⚠️ Minus:** PostgreSQL uchun maxsus sozlangan (`xmin`).

---

### 7. Ko'p Ijarachilik (Multi-Tenancy Izolyatsiyasi)

SaaS yoki ko'p tashkilotli tizimlarda turli mijozlar ma'lumotlarini qat'iy ajratib turish.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. Entitiyga interfeys beriladi:
public class Invoice : ModelBase<long>, ITenantEntity
{
    public long? TenantId { get; set; }
    public decimal Amount { get; set; }
}

// 2. CoreDbContext avtomatik faqat joriy ijarachi ma'lumotlarini ko'rsatadi.
// Vaqtincha boshqa ijarachiga o'tish yoki filtrni ochish:
using (tenantContext.Change(otherTenantId)) { ... }
using (tenantContext.DisableTenantFilter()) { ... }
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Ma'lumotlarning boshqa tashkilotga oqib ketish (Data Leakage) xavfi yo'qoladi. `(TenantId, IsDelete)` indeksi avtomatik yaratiladi (Sequential Scan muammosi yo'qoladi).
- **⚠️ Minus:** Umumiy hisobotlar olishda filtrni qo'lda ochish kerak.

---

### 8. MassTransit + Transactional Outbox (RabbitMQ)

Xabarlarni ma'lumotlar bazasi tranzaksiyasi bilan bitta atomik tranzaksiyada saqlash (Dual-Write muammosini yo'qotadi).

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. Program.cs da ulash:
builder.Services.AddCoreMassTransit<MyDbContext>(builder.Configuration, bus =>
{
    bus.AddConsumer<OrderCreatedConsumer>();
});

// 2. Servis ichida Publish qilish:
await publishEndpoint.Publish(new OrderCreatedIntegrationEvent { OrderId = 123 }, ct);
await dbContext.SaveChangesAsync(ct); // Outbox va Baza bitta atomik tranzaksiyada commit bo'ladi!
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** Tarmoq uzilsa ham xabar yo'qolmaydi, baza commit bo'lsagina xabar ketadi. Eksponentsial qayta urinish (Retry) va Tenant avto-uzatish bor.
- **⚠️ Minus:** Baza ichida Outbox/Inbox jadvallari yaratiladi va fonda MassTransit outbox service ishlaydi.

---

### 9. Code-First gRPC (`protobuf-net.Grpc`)

Mikroservislararo aloqani sekin REST o'rniga tezyurar binary gRPC orqali amalga oshirish. `.proto` fayllar yozish shart emas!

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// 1. Umumiy shartnoma interfeysi:
[ServiceContract]
public interface IBillingGrpcService
{
    [OperationContract]
    Task<BillingDto> GetBillingAsync(GetBillingRequest request, CallContext context = default);
}

// 2. Server servisda:
builder.Services.AddCoreGrpcServer();
app.MapGrpcService<BillingGrpcService>();

// 3. Chaqiruvchi (Client) servisda:
builder.Services.AddCoreGrpcClient<IBillingGrpcService>("http://billing-service:5001");
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** REST ga nisbatan 5-10 baravar tezroq binary oqim. Deadline (10 soniya), CorrelationId, TenantId va UserId avtomatik uzatiladi.
- **⚠️ Minus:** Faqat .NET servislari o'rtasida ishlatish eng qulay.

---

### 10. Xavfsizlik Sarlavhalari, Rate Limiting va Siqish

Tashqi hujumlardan himoya va tarmoq trafigini siqish.

#### Qayerda chaqiriladi va qanday ishlatiladi?
```csharp
// Program.cs da:
builder.Services.AddCoreRateLimiting();
builder.Services.AddCoreCompression();

app.UseCoreSecurityHeaders(); // X-Frame-Options: DENY, X-Content-Type-Options: nosniff
app.UseResponseCompression(); // Brotli + Gzip
app.UseRateLimiter();         // IP bo'yicha 100 req/s sliding window
```

#### ⚖️ Plus va Minuslari:
- **✅ Plus:** DoS hujumlariga qarshi avtomatik cheklov, tarmoq trafigini siqish, Clickjacking va XSS dan himoya.
- **⚠️ Minus:** Siqish jarayoni server protsessoriga (CPU) ozgina yuklama beradi.

---

## 🧪 Sinovlarni Ishga Tushirish (Unit Tests)

```bash
dotnet test Core.sln
```
Barcha 16 ta unit testlar muvaffaqiyatli o'tadi.
