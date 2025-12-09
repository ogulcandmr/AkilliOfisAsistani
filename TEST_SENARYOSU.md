# 🧪 Test Senaryosu - AI Destekli Ofis Görev Otomasyonu

## 📋 Test Öncesi Hazırlık

### 1. Veritabanı Hazırlığı
1. Supabase Dashboard'a giriş yap
2. SQL Editor'ü aç
3. `TEST_VERILERI.sql` dosyasındaki tüm SQL kodunu çalıştır
4. Verilerin eklendiğini kontrol et (script'in sonundaki SELECT sorguları)

### 2. Uygulama Ayarları
- `App.config` dosyasında Supabase ve OpenAI bilgilerinin doğru olduğundan emin ol
- Projeyi derle ve çalıştır (F5)

---

## 🎬 TEST SENARYOSU 1: Yönetici Paneli ve AI Önerileri

### Adımlar:
1. **Giriş Yap:**
   - Kullanıcı adı: `manager`
   - Şifre: `123`
   - ✅ Login ekranı açılmalı ve giriş başarılı olmalı

2. **Yönetici Paneline Git:**
   - Menüden "Yönetici Paneli" seç
   - ✅ Panel açılmalı, görevler ve çalışanlar listelenmeli

3. **Görev Listesini İncele:**
   - Görevler tablosunda görevler görünmeli
   - ✅ En az 15 görev olmalı
   - ✅ Görevlerin durumları (Bekliyor, Yapılıyor, Tamamlandı) görünmeli

4. **Çalışan İş Yükü Kontrolü:**
   - Çalışanlar tablosuna bak
   - ✅ İş yükü yüzdeleri renkli görünmeli (kırmızı: >80%, turuncu: >60%, sarı: >40%, yeşil: <40%)
   - ✅ Zeynep Şahin'in iş yükü yüksek olmalı (35/40)

5. **Anomali Tespiti:**
   - Anomali listesine bak
   - ✅ Gecikmiş görevler görünmeli
   - ✅ "Eski Proje: Sistem Migrasyonu" gecikmiş olmalı

6. **Yeni Görev Oluştur ve AI Önerisi Al:**
   - "Yeni Görev" butonuna tıkla
   - Başlık: `Mobil Uygulama Tasarımı`
   - Açıklama: `iOS ve Android için mobil uygulama UI/UX tasarımı yapılacak`
   - Öncelik: `Yüksek`
   - "AI Öneri Al" butonuna tıkla
   - ✅ AI en uygun çalışanı önermeli (muhtemelen Mehmet Kaya - React/JavaScript bilgisi var)
   - Önerilen çalışanı seç ve kaydet
   - ✅ Görev oluşturulmalı

---

## 🎬 TEST SENARYOSU 2: Sesli Yönetici Modülü

### Adımlar:
1. **Sesli Yönetici Modülünü Aç:**
   - Menüden "Sesli Yönetici" seç
   - ✅ Sesli yönetici formu açılmalı

2. **Mikrofon İzinlerini Kontrol Et:**
   - Windows mikrofon izinlerinin açık olduğundan emin ol
   - ✅ Form açılmalı, "Hazır" durumu görünmeli

3. **Sesli Görev Atama:**
   - "Dinlemeyi Başlat" butonuna tıkla
   - ✅ "Dinliyorum, komutunuzu söyleyin." sesli mesajı gelmeli
   - Mikrofona şunu söyle: **"Haftaya Çarşamba'ya kadar Ayşe, Yıllık Bütçe Sunumu'nu hazırlasın, öncelik yüksek"**
   - ✅ Komut tanınmalı ve işlenmeli
   - ✅ Görev oluşturulmalı (Ayşe Demir'e atanmış, yüksek öncelikli)

4. **Sesli Rapor Sorgulama:**
   - "Dinlemeyi Başlat" butonuna tekrar tıkla
   - Mikrofona şunu söyle: **"Bana bu hafta bitmeyen işleri listele"**
   - ✅ Rapor görünmeli
   - ✅ Bitmeyen işlerin listesi ve sayısı gösterilmeli
   - ✅ Sesli okuma yapılmalı

---

## 🎬 TEST SENARYOSU 3: Çalışan Paneli ve Kanban

### Adımlar:
1. **Çalışan Olarak Giriş:**
   - Uygulamayı kapat ve yeniden aç
   - Kullanıcı adı: `employee1` (Ayşe Demir)
   - Şifre: `123`
   - ✅ Giriş başarılı olmalı

2. **Günlük Brifing Kontrolü:**
   - Çalışan Paneli açılmalı
   - ✅ Sağ tarafta "Günlük Brifing" paneli görünmeli
   - ✅ AI tarafından oluşturulmuş kişiselleştirilmiş brifing görünmeli
   - ✅ Bugünkü görevler ve toplantılar listelenmeli

3. **Kanban Panosu Kullanımı:**
   - Sol tarafta Kanban panosu görünmeli
   - ✅ 3 sütun olmalı: "Bekliyor", "Yapılıyor", "Tamamlandı"
   - ✅ Ayşe Demir'in görevleri görünmeli

4. **Görev Durumu Değiştirme (Sürükle-Bırak):**
   - "Bekliyor" sütunundan bir görevi seç
   - Sürükle ve "Yapılıyor" sütununa bırak
   - ✅ Görev durumu güncellenmeli
   - ✅ Veritabanında status değişmeli

5. **Görevi Tamamla:**
   - "Yapılıyor" sütunundan bir görevi seç
   - "Tamamlandı" sütununa sürükle
   - ✅ Görev tamamlanmış olmalı
   - ✅ CompletedDate otomatik doldurulmalı

6. **AI Alt Görev Sihirbazı:**
   - "AI Alt Görev" butonuna tıkla
   - Açılan pencerede şunu yaz: **"Mobil Uygulama Yap"**
   - ✅ AI görevi alt görevlere bölmeli
   - ✅ En az 3-4 alt görev önerilmeli (Login ekranı, API, Tasarım, vb.)
   - ✅ Her alt görev için tahmini süre gösterilmeli

---

## 🎬 TEST SENARYOSU 4: Bildirim Sistemi

### Adımlar:
1. **Yönetici Olarak Giriş:**
   - Kullanıcı adı: `manager`
   - Şifre: `123`

2. **Yaklaşan Deadline Testi:**
   - Yeni bir görev oluştur
   - Teslim tarihi: **Bugünden 2 saat sonra**
   - Öncelik: **Yüksek**
   - ✅ 2 saat sonra bildirim çıkmalı
   - ✅ Sağ alt köşede uyarı görünmeli

3. **Gecikmiş Görev Bildirimi:**
   - "Eski Proje: Sistem Migrasyonu" görevi zaten gecikmiş
   - ✅ Uygulama açıldığında gecikmiş görev bildirimi çıkmalı

4. **Toplantı Hatırlatması:**
   - Test verilerinde bugün saat 09:00'da bir toplantı var
   - ✅ Toplantıdan 15 dakika önce (08:45) hatırlatma çıkmalı
   - ✅ Bildirim mesajı görünmeli

---

## 🎬 TEST SENARYOSU 5: AI Özellikleri ve Anomali Tespiti

### Adımlar:
1. **Yönetici Paneline Git:**
   - Kullanıcı: `manager`

2. **Anomali Listesini Kontrol Et:**
   - Anomali panelinde listeye bak
   - ✅ Gecikmiş görevler görünmeli
   - ✅ "Bu görev 5 gün gecikmiş. Müdahale gerekli." gibi mesajlar olmalı

3. **İş Yükü Aşırı Yüklenme Tespiti:**
   - Çalışanlar tablosuna bak
   - ✅ Zeynep Şahin'in iş yükü %87.5 (35/40) - kırmızı görünmeli
   - ✅ Anomali listesinde "Zeynep Şahin çok yoğun" uyarısı olmalı

4. **AI Personel Atama Önerisi:**
   - Yeni görev oluştur: "Python ile Veri Analizi"
   - Açıklama: "Büyük veri setlerini analiz et ve raporla"
   - "AI Öneri Al" butonuna tıkla
   - ✅ AI Ayşe Demir'i önermeli (Python, AI, Data Analysis yetenekleri var)
   - ✅ Öneri nedeni açıklanmalı

---

## 🎬 TEST SENARYOSU 6: Görev Detayları ve İstatistikler

### Adımlar:
1. **Görev Detayını Görüntüle:**
   - Yönetici Paneli'nde bir göreve çift tıkla
   - ✅ Görev detay formu açılmalı
   - ✅ Tüm bilgiler görünmeli (Başlık, Açıklama, Durum, Öncelik, Teslim Tarihi, vb.)

2. **İstatistikleri Kontrol Et:**
   - Yönetici Paneli'nde grafikler paneli var (şu an placeholder)
   - ✅ İstatistikler paneli görünmeli
   - ✅ DevExpress Chart eklendiğinde grafikler görünecek

---

## ✅ Beklenen Sonuçlar

### Başarı Kriterleri:
- ✅ Tüm formlar açılıyor
- ✅ Veritabanı bağlantısı çalışıyor
- ✅ Görevler listeleniyor ve oluşturuluyor
- ✅ AI önerileri çalışıyor
- ✅ Sesli komutlar tanınıyor (mikrofon izni varsa)
- ✅ Kanban panosu sürükle-bırak çalışıyor
- ✅ Bildirimler zamanında çıkıyor
- ✅ Anomali tespiti çalışıyor
- ✅ Günlük brifing oluşturuluyor

### Bilinen Sınırlamalar:
- ⚠️ DevExpress kontrolleri henüz eklenmedi (placeholder'lar var)
- ⚠️ Sesli komutlar için Windows Speech Recognition gerekli
- ⚠️ AI özellikleri için internet bağlantısı gerekli
- ⚠️ Bildirimler şu an MessageBox olarak gösteriliyor (DevExpress Toast eklenebilir)

---

## 🐛 Sorun Giderme

### Sesli komutlar çalışmıyor:
- Windows Ayarlar > Gizlilik > Mikrofon izinlerini kontrol et
- Sistem dilinin Türkçe olması önerilir

### AI önerileri gelmiyor:
- OpenAI API key'in geçerli olduğundan emin ol
- İnternet bağlantını kontrol et
- API quota limitini kontrol et

### Veritabanı bağlantı hatası:
- Supabase URL ve API key'in doğru olduğundan emin ol
- RLS (Row Level Security) politikalarını kontrol et
- SQL script'in başarıyla çalıştığından emin ol

---

## 📝 Test Raporu Şablonu

Test sonuçlarını buraya not edebilirsin:

| Senaryo | Durum | Notlar |
|---------|-------|--------|
| Senaryo 1: Yönetici Paneli | ⬜ | |
| Senaryo 2: Sesli Yönetici | ⬜ | |
| Senaryo 3: Çalışan Paneli | ⬜ | |
| Senaryo 4: Bildirimler | ⬜ | |
| Senaryo 5: AI Özellikleri | ⬜ | |
| Senaryo 6: Görev Detayları | ⬜ | |

**Genel Değerlendirme:**
- Çalışan Özellikler:
- Hatalar:
- Öneriler:

