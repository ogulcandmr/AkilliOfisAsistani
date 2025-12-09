# ⚡ Hızlı Başlangıç - 5 Dakikada Test Et!

## 🚀 Adım 1: Veritabanına Test Verilerini Ekle (2 dakika)

1. Supabase Dashboard'a git: https://supabase.com/dashboard
2. Projeni seç
3. Sol menüden **"SQL Editor"** seç
4. **"New Query"** butonuna tıkla
5. `TEST_VERILERI.sql` dosyasını aç ve **TÜM İÇERİĞİNİ** kopyala
6. SQL Editor'e yapıştır
7. **"Run"** butonuna tıkla (veya F5)
8. ✅ Başarılı mesajı görmelisin

## 🎯 Adım 2: Uygulamayı Çalıştır (1 dakika)

1. Visual Studio'da projeyi aç
2. `F5` ile çalıştır
3. ✅ Login ekranı açılmalı

## 🧪 Adım 3: Hızlı Test (2 dakika)

### Test 1: Giriş
- Kullanıcı: `manager` / Şifre: `123`
- ✅ Giriş başarılı olmalı

### Test 2: Yönetici Paneli
- Menüden "Yönetici Paneli" seç
- ✅ Görevler ve çalışanlar görünmeli

### Test 3: Yeni Görev
- "Yeni Görev" butonuna tıkla
- Başlık: `Test Görevi`
- "AI Öneri Al" butonuna tıkla
- ✅ AI bir çalışan önermeli

### Test 4: Çalışan Paneli
- Uygulamayı kapat, yeniden aç
- Kullanıcı: `employee1` / Şifre: `123`
- Menüden "Çalışan Paneli" seç
- ✅ Kanban panosu ve brifing görünmeli

## ✅ Başarılı!

Eğer yukarıdaki testler çalışıyorsa, sistem hazır! 🎉

Detaylı test için `TEST_SENARYOSU.md` dosyasına bak.

