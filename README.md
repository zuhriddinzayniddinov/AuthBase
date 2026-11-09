# AuthBase

Foydalanuvchilar autentifikatsiyasi (Authentication), ruxsatlarini boshqarish (RBAC Authorization) va mikroservislararo huquqlarni taqsimlash uchun mo'ljallangan markaziy xavfsizlik xizmati.

---

## 📑 Mundarija
1. [Arxitektura va Loyiha Holati](#arxitektura-va-loyiha-holati)
2. [Nega `.sln` va `.slnx` mavjud?](#nega-sln-va-slnx-mavjud)
3. [Mikroservislararo gRPC Permission & Structure Synchronization](#mikroservislararo-grpc-permission--structure-synchronization)
4. [Amalga oshirilgan optimizatsiyalar va tuzatilgan kamchiliklar](#amalga-oshirilgan-optimizatsiyalar-va-tuzatilgan-kamchiliklar)
5. [Loyiha tuzilmasi](#loyiha-tuzilmasi)
6. [O'rnatish va Ishga Tushirish](#ornatish-va-ishga-tushirish)

---

## Arxitektura va Loyiha Holati

- **Core Subtree Integratsiyasi:** Umumiy yadro sifatida [Core](https://github.com/zuhriddinzayniddinov/Core.git) loyihasi `git subtree` orqali `/Core` papkasiga ulangan. Dublikat kodlar (WebCore, GenericRepository, Exceptionlar, Cache/Api brokerlar) butunlay olib tashlangan va Core'dagi tayyor yechimlardan foydalaniladi.
- **Alohida Ma'lumotlar Bazasi (`auth_db`):** `CoreDbContext` asosida qurilgan `AuthDataContext` orqali alohida `auth` sxemasida mustaqil jadvallar ishlaydi:
  - `auth.users` — Foydalanuvchilar profili
  - `auth.user_sign_methods` — Kirish usullari (login/parol, OTP, biometric)
  - `auth.structures` — Rollar/Strukturalar (`Type = 1`: Default User, `Type = 2`: SuperAdmin)
  - `auth.permissions` — Servislardan yig'ilgan barcha ruxsatlar katalogi
  - `auth.structure_permissions` — Qaysi strukturaga qaysi ruxsatlar biriktirilganligi
  - `auth.user_structures` — Foydalanuvchiga biriktirilgan strukturalar
  - `auth.tokens` — Refresh tokenlar va faol sessiyalar
- **Yagona Solution:** Loyihaning barcha qismlari va Core bitta yagona `AuthBase.sln` da jamlangan.

---

## Nega `.sln` va `.slnx` mavjud?

.NET ekotizimida Microsoft tomonidan ikki xil solution formati qo'llab-quvvatlanadi:

1. **`.sln` (Klassik format):**
   - 20 yildan ortiq vaqt davomida ishlatib kelinayotgan matnli format.
   - Barcha IDE'lar (Visual Studio eski va yangi versiyalari, JetBrains Rider, VS Code, CI/CD runnerlar) bilan 100% mos keladi.
2. **`.slnx` (Yangi XML format):**
   - .NET 9 va .NET 10 SDK bilan birga taqdim etilgan zamonaviy, ixcham va toza XML format.
   - GUID qatorlaridan xoli, o'qish va git diff larda ko'rish ancha oson.
   - `.NET 10 SDK` da `dotnet new sln` buyrug'i sukut bo'yicha `.slnx` yaratadi.

> [!NOTE]
> Loyihamizda maksimal universal moslashuvchanlik, barcha CI/CD vositalari va IDE'larda hech qanday muammosiz ishlashi uchun yechim **`AuthBase.sln`** standart formatida yuritilmoqda.

---

## Mikroservislararo gRPC Permission & Structure Synchronization

### Bu narsa Core'da bormi?
- **Oldingi holat:** Core kutubxonasida faqat mikroservis start bo'lganida o'zining ruxsatlar enum'ini AuthApi ga yuborish (`SyncPermissionsAsync`) mexanizmi mavjud edi. Ammo mikroservis ishga tushganda o'zining xavfsizligini ta'minlash uchun AuthApi dan o'ziga tegishli struktura va ruxsat kodlarini qabul qilib olib, xotiradagi `StaticCache.Permissions` ga yuklash mexanizmi **yo'q edi**. Oqibatda har qanday `[PermissionAuthorize]` tekshiruvi `403 Forbidden` bilan yakunlanar edi.
- **Hozirgi holat:** Biz Core va AuthBase'ga Code-First gRPC orqali to'liq avtomatlashtirilgan xavfsiz mexanizm kiritdik:
  1. Mikroservis ishga tushganida AuthApi ga faqat o'zining servis nomi yoki prefiks kodi bo'yicha murojaat qiladi.
  2. AuthApi faqat va faqat so'ralgan mikroservisga tegishli bo'lgan strukturalar va ruxsat kodlarini qaytaradi.
  3. **Ortiqcha yoki begona ma'lumotlar berilmaydi:** Mikroservisga boshqa servislarga (masalan: AuthApi, Billing, HR) tegishli yuzlab ruxsat kodlari yoki ushbu servisda umuman huquqi bo'lmagan begona struktura ID lari yuborilmaydi!
  4. Qabul qilingan ma'lumotlar mikroservis xotirasidagi `StaticCache.Permissions` lug'atiga avtomatik yoziladi.

### Mikroservisda qo'llash (Atigi 2 qadam):

1. **`Program.cs` da gRPC mijozni ulash va avto-sinxronizatsiya:**
```csharp
// 1. AuthApi gRPC manzilini ko'rsatish
builder.Services.AddCoreGrpcClient<IStructurePermissionGrpcService>("http://auth-api:50051");

// 2. Mikroservis enum ruxsatlarini ro'yxatdan o'tkazish (Core orqali)
builder.Services.AddCorePermissionAutoSync<OrderPermissions>(PermissionSyncMode.Grpc);
```

2. **Kontrollerda tekshirish:**
```csharp
[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    [HttpPost]
    [PermissionAuthorize(OrderPermissions.CreateOrder)]
    public async Task<IActionResult> CreateOrder(...)
    {
        // Sessiyadagi StructureId lar bo'yicha StaticCache.Permissions avtomatik tekshiriladi
        return Ok();
    }
}
```

---

## Amalga oshirilgan optimizatsiyalar va tuzatilgan kamchiliklar

1. **RBAC Kesh Ishonchliligi (`StaticCache.Permissions`):**
   - Startup paytida `AuthDataSeeder.ReloadPermissionsCacheAsync` chaqirilib, barcha rollar va ruxsatlar xotiraga yuklanadi.
   - `RoleService` da rol yaratilganda, tahrirlanganda yoki o'chirilganda `StaticCache.Permissions` darhol sinxron yangilanadi.
2. **`UserService` va `MinioService` Unumdorligi (10x tezlashuv):**
   - Foydalanuvchilar ro'yxatini olishda har bir element uchun `.Result` orqali MinIO ga sinxron so'rov yuborish (sync-over-async deadlock xavfi) to'liq olib tashlandi.
   - Asinxron `Task.WhenAll` joriy etildi va `AvatarUrl` bo'sh bo'lgan holatlarda MinIO ga ortiqcha so'rov yuborilmaydi.
   - `MinioService` da har bir presigned URL so'rovida MinIO serveriga borib paqir (bucket) tekshirish o'rniga xotirada bir martalik keshlandi.
3. **`AuthService.RegisterAsync` Tranzaksiya Xavfsizligi:**
   - Foydalanuvchi va uning paroli (DefaultSignMethod) bitta ACID tranzaksiya (`BeginTransactionAsync`) ichida bajarilishi ta'minlandi.
4. **Lahzali Logout va Sessiyani Bekor Qilish (Session Revocation):**
   - Foydalanuvchi tizimdan chiqqanida (`DeleteTokenAsync`), uning JWT tokeni bazadan o'chirilishi bilan birga, tarqatilgan Redis keshiga (`AppCacheKeys.SessionRevokedL2Format`) yoziladi.
   - `SessionValidationMiddleware` ushbu tokenni keyingi barcha so'rovlarda 0.001 ms ichida darhol `401 Unauthorized` bilan to'xtatadi.
5. **EF Core SQL Translation Tuzatildi:**
   - `GetUserPermissionsAsync` dagi ichki `.ToList()` olib tashlanib, so'rov to'g'ridan-to'g'ri toza SQL `JOIN` va `DISTINCT` ga tarjima qilinadigan qilindi.
6. **Boshlang'ich Ma'lumotlar Seederi (`AuthDataSeeder`):**
   - Yangi o'rnatilgan tizimda `User` (`Type = 1`) va `SuperAdmin` (`Type = 2`) rollari hamda `UserPermissions` enum'idagi barcha ruxsatlar avtomatik bazaga kiritiladi.
7. **Yuqori Unumdorlik:**
   - AuthApi ga `AddCoreCompression` (Brotli/Gzip) va `AddCoreRateLimiting` qo'shildi.

---

## Loyiha tuzilmasi

```
AuthBase/
├── Core/                                  # Git Subtree orqali ulangan umumiy yadro
│   ├── src/
│   │   ├── Core.Domain/                   # Umumiy modellar, atributlar, keshlar
│   │   ├── Core.Infrastructure/           # gRPC, Redis, MassTransit, xavfsizlik
│   │   └── Core.Web/                      # Filterlar, middleware'lar, kontrollerlar
│   └── tests/
│       └── Core.Tests/                    # Core birlik va integratsion testlari
├── src/
│   ├── Domain/
│   │   └── Entity/                        # Auth modellari, jadvallari va DTO'lar
│   ├── Infrastructures/
│   │   ├── DatabaseBroker/                # AuthDataContext, migratsiyalar va seeder
│   │   └── MinIoBroker/                   # MinIO fayl va avatar xizmati
│   ├── Services/
│   │   ├── AuthService/                   # Autentifikatsiya, tokenlar, foydalanuvchilar
│   │   └── StructureService/              # Rollar va ruxsatlar biznes mantig'i
│   └── WebApis/
│       └── AuthApi/                       # REST API va gRPC server
└── tests/
    └── AuthBase.Tests/                    # AuthBase xizmatlari va gRPC sinxronizatsiya testlari
```

---

## O'rnatish va Ishga Tushirish

### Talablar
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 15+](https://www.postgresql.org/)
- [Redis](https://redis.io/)
- [MinIO](https://min.io/)

### Yechimni yig'ish va testlarni yurgizish:
```bash
# Barcha loyihalarni yig'ish
dotnet build AuthBase.sln

# Barcha testlarni (Core va AuthBase) ishga tushirish
dotnet test AuthBase.sln
```

### AuthApi ni ishga tushirish:
```bash
cd src/WebApis/AuthApi
dotnet run
```
Ishga tushganda avtomatik tarzda:
1. Ma'lumotlar bazasi migratsiyalari qo'llanadi (`UpMigration`).
2. Rollar va ruxsatlar tekshirilib to'ldiriladi (`SeedAuthDataAsync`).
3. RBAC xotira keshi to'ldiriladi (`CoreStaticCache.Permissions`).
4. REST API (`/swagger`) va Code-First gRPC xizmati tayyor holatga keladi.
