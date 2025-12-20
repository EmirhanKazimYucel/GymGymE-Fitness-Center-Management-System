# GymGyme - AI Destekli Fitness Paneli

GymGyme, kullanicilarin egzersiz ve beslenme hedeflerini tek panelden yonetmesini saglayan ASP.NET Core 9 MVC tabanli bir web uygulamasidir. Uygulama PostgreSQL, Entity Framework Core, seans tabanli kimlik dogrulama ve OpenAI servisleri (GPT-4.1-mini + DALL-E) uzerinden sahsilesmis diyet planlari ile tek parca gorsel kolajlar uretir.

## Onemli Ozellikler
- **Kimlik Dogrulama & Yetkilendirme:** Email + sifre tabanli kayit/giris, 8 saatlik session saklama, rol bazli (Admin, User).
- **Kullanici Paneli:** Randevu durumlari, haftalik liderlik tablosu, aktivite filtreleri ve profil formu (avatar yukleme dahil).
- **Yapay Zeka Diyet Plani:** Profil verilerinden beslenen GPT tabanli JSON plan ve DALL-E kolaj gorseli; hata durumunda standart icerik + yerel fallback SVG.
- **Randevu Modulu:** Antrenor/hizmet secimi, saat dilimi takibi, admin onay akisleri.
- **Admin Paneli:** Randevu karar ekranlari, antrenor ve hizmet CRUD, salon bilgileri ve calisma saatleri; Chart.js tabanli istatistikler.
- **Konfigurasyon & Gozlemlenebilirlik:** OpenAI zaman asimi/yol ayarlari, dosya yuklemeleri icin 2 MB limit, detayli hata mesajlari.

## Mimari Ozet
| Yol | Amac |
| --- | --- |
| Controllers/AccountController.cs | Kayit, giris, cikis ve sekmeli Auth sayfasi.
| Controllers/DashboardController.cs | Kullanici paneli, profil formu, AI diyeti ve gorsel akis.
| Controllers/AdminController.cs | Admin paneli, randevu karar akisi, antrenor/hizmet CRUD, salon bilgileri.
| Services/OpenAiDietService.cs | GPT-4.1-mini uzerinden JSON diyet uretir (chat/completions).
| Services/OpenAiImageService.cs | DALL-E istegini varyant promptlarla gonderir, base64 kolaj dondurur.
| Data/FitnessContext.cs | EF Core DbContext; AppUser, AppointmentRequest, Coach, Service vb.
| Views/* | Razor tabanli UI (ozellikle _DashboardLayout, Dashboard/DietPlan.cshtml, Admin/Panel.cshtml).
| wwwroot/ | CSS, JS ve DALL-E fallback gorseli (images/diet/menu-collage.svg).

## Gereksinimler
- .NET SDK 9.0+
- PostgreSQL 15+ (veya uyumlu bir surum)
- OpenAI API anahtari (Chat & Image yetkili)
- Node.js (opsiyonel; yalnizca frontend derlemesi gerekiyorsa)

## Konfigurasyon
1. `appsettings.json` veya User Secrets uzerinden veritabani baglantisini ayarlayin:
   ```json
   "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=gymgyme;Username=postgres;Password=..."
   }
   ```
2. OpenAI secenekleri icin (`Configuration/OpenAiOptions.cs`) su anahtarlari ekleyin:
   ```json
   "OpenAi": {
     "ApiKey": "sk-...",
     "Model": "gpt-4.1-mini",
     "ImageModel": "gpt-image-1",
     "ImageSize": "1024x1024",
     "TimeoutSeconds": 40,
     "MaxOutputTokens": 800,
     "Temperature": 0.4
   }
   ```
   > API anahtari bos birakilirsa AI uretimi devre disi kalir.
3. `appsettings.Development.json` icinde SMTP, cookie vb. degerleri doldurun (gerekiyorsa).

## Veritabani Hazirligi
```bash
dotnet tool install --global dotnet-ef   # gerekirse
dotnet ef database update                # Migrations/* uygulanir
```

## Uygulamayi Calistirma
```bash
dotnet restore
dotnet run
```
- Varsayilan rota: https://localhost:5001
- Giris adresi: /Account/Login

### Varsayilan Admin Hesabi
| Email | Sifre |
| --- | --- |
| g231210374@sakarya.edu.tr | sau |
> Program.cs icindeki tohumlama blokunu guclendirerek veya sifreyi degistirerek yayin oncesi guncelleyiniz.

## Yapay Zeka Akisi
1. DashboardController.DietPlan kullanici profil verilerinden `DietPlanRequestContext` uretir.
2. OpenAiDietService JSON cevabi parse ederek `DietPlanViewModel` icine uygular.
3. OpenAiImageService ayni baglamdan tek bir kolaj gorseli ister; hata durumunda `wwwroot/images/diet/menu-collage.svg` fallback'i dondurur.
4. Metin ve gorsel icerik Razor tarafinda (Dashboard/DietPlan.cshtml) kart semalariyla sunulur.

## Kod Stili ve Kalite
- Nullable referanslar acik (C# 11).
- `RoleAuthorize` filtresi kritik sayfalari korur.
- HTTP istemcileri `AddHttpClient` ile konfigure edilir; zaman asimlari 5-120 sn arasinda tutulur.
- Avatar yuklemeleri icin 2 MB limit ve MIME kontrolu vardir.
- Tum mutasyon aksiyonlarinda `[ValidateAntiForgeryToken]` bulunur.

## Faydalı Komutlar
```bash
# Yeni migration
dotnet ef migrations add AddNewFeature

# Build/test
dotnet build
dotnet test   # (test projesi eklendiginde)
```

