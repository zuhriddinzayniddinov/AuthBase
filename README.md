# AuthBase

Foydalanuvchilar autentifikatsiyasi, avtorizatsiyasi va huquqlarni boshqarish uchun mo'ljallangan mikroservis arxitekturasi.

## Xususiyatlar
- **Core Subtree integratsiyasi:** Umumiy arxitektura qatlami sifatida [Core](https://github.com/zuhriddinzayniddinov/Core.git) loyihasi `git subtree` orqali ulangan.
- **Alohida ma'lumotlar bazasi (`auth_db`):** `CoreDbContext` asosidagi mustaqil `AuthDataContext` orqali to'liq izolyatsiya qilingan Auth sxemasi (`permissions`, `structures`, `structure_permissions`, `users`, `user_structures`, `user_sign_methods`, `tokens`).
- **Yagona Solution:** Core va AuthBase barcha loyihalari yagona `Auth.sln` yechimida jamlangan.
- **Dublikatsiz toza arxitektura:** Barcha umumiy middleware'lar, filterlar, exceptionlar, helperlar, caching va xavfsizlik funksiyalari `Core` kutubxonasidan olinadi.

## Loyihalar tuzilmasi
- **`Core/`** (Git Subtree):
  - `Core.Domain`
  - `Core.Infrastructure`
  - `Core.Web`
  - `Core.Tests`
- **`src/`** (AuthBase):
  - `Domain/Entity` - Auth entitiylari va DTO'lari
  - `Infrastructures/DatabaseBroker` - `AuthDataContext`, repozitoriylar va migratsiyalar
  - `Infrastructures/MinIoBroker` - MinIO fayl va avatar integratsiyasi
  - `Services/AuthService` - Autentifikatsiya va foydalanuvchi biznes mantig'i
  - `Services/StructureService` - Rollar va huquqlar biznes mantig'i
  - `WebApis/AuthApi` - RESTful Web API endpointlari

## O'rnatish va ishga tushirish
### Talablar
- .NET 10 SDK
- PostgreSQL
- Redis
- MinIO

### Ishga tushirish
```bash
# Loyihani build qilish
dotnet build Auth.sln

# Testlarni yurgizish
dotnet test Auth.sln

# Docker Compose orqali barcha bog'liqliklar bilan ishga tushirish
docker-compose up -d
```
