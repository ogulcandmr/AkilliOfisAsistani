# 🚀 Kurulum Rehberi - AI Destekli Ofis Görev Otomasyonu

## Adım 1: Visual Studio'da Projeyi Açma

1. Visual Studio 2019 veya üzeri sürümü açın
2. `File > Open > Project/Solution` ile `OfisAsistan.sln` dosyasını açın
3. Solution Explorer'da projeyi göreceksiniz

## Adım 2: NuGet Paketlerini Yükleme

1. Solution Explorer'da projeye sağ tıklayın
2. `Manage NuGet Packages...` seçeneğini tıklayın
3. `Browse` sekmesinde "Newtonsoft.Json" arayın
4. Versiyon 13.0.3'ü seçin ve `Install` butonuna tıklayın
5. Kurulum tamamlanana kadar bekleyin

## Adım 3: DevExpress Kurulumu (ÖNEMLİ)

### DevExpress'i İndirme ve Kurma:
1. [DevExpress](https://www.devexpress.com/) web sitesinden DevExpress'i indirin
2. Kurulum dosyasını çalıştırın ve kurulumu tamamlayın
3. Visual Studio'yu kapatın (eğer açıksa)

### DevExpress'i Visual Studio'ya Entegre Etme:
1. Visual Studio'yu yönetici olarak çalıştırın
2. `Tools > DevExpress > Register Controls` menüsünü tıklayın
3. Kurulum tamamlanana kadar bekleyin

### DevExpress Toolbox'a Ekleme:
1. Visual Studio'da `View > Toolbox` menüsünü açın
2. Toolbox'a sağ tıklayın ve `Choose Items...` seçin
3. `.NET Framework Components` sekmesinde DevExpress kontrollerini seçin:
   - DevExpress.XtraGrid.GridControl
   - DevExpress.XtraCharts.ChartControl
   - DevExpress.XtraBars.Ribbon.RibbonControl
   - DevExpress.XtraEditors.TileView
4. `OK` butonuna tıklayın

## Adım 4: Supabase Veritabanı Kurulumu

### Supabase Hesabı Oluşturma:
1. [https://supabase.com](https://supabase.com) adresine gidin
2. `Start your project` butonuna tıklayın
3. GitHub veya email ile kayıt olun
4. Yeni bir proje oluşturun (proje adı: `ofis-asistan` gibi)

### Veritabanı Tablolarını Oluşturma:
1. Supabase dashboard'da `SQL Editor` sekmesine gidin
2. `New Query` butonuna tıklayın
3. Aşağıdaki SQL kodunu yapıştırın ve `Run` butonuna tıklayın:

```sql
-- Employees tablosu
CREATE TABLE IF NOT EXISTS employees (
    id SERIAL PRIMARY KEY,
    first_name VARCHAR(100) NOT NULL,
    last_name VARCHAR(100) NOT NULL,
    email VARCHAR(255) UNIQUE,
    department_id INTEGER,
    position VARCHAR(100),
    skills TEXT,
    current_workload INTEGER DEFAULT 0,
    max_workload INTEGER DEFAULT 40,
    is_active BOOLEAN DEFAULT true,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Departments tablosu
CREATE TABLE IF NOT EXISTS departments (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100) NOT NULL,
    description TEXT,
    manager_id INTEGER,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Tasks tablosu
CREATE TABLE IF NOT EXISTS tasks (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    assigned_to_id INTEGER,
    created_by_id INTEGER,
    created_date TIMESTAMP DEFAULT NOW(),
    due_date TIMESTAMP,
    status INTEGER DEFAULT 0,
    priority INTEGER DEFAULT 1,
    department_id INTEGER,
    skills_required TEXT,
    estimated_hours INTEGER,
    actual_hours INTEGER DEFAULT 0,
    completed_date TIMESTAMP,
    notes TEXT,
    is_anomaly BOOLEAN DEFAULT false,
    anomaly_reason TEXT
);

-- Meetings tablosu
CREATE TABLE IF NOT EXISTS meetings (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255) NOT NULL,
    description TEXT,
    start_time TIMESTAMP NOT NULL,
    end_time TIMESTAMP NOT NULL,
    organizer_id INTEGER,
    location VARCHAR(255),
    attendee_ids TEXT,
    is_reminder_sent BOOLEAN DEFAULT false,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Users tablosu
CREATE TABLE IF NOT EXISTS users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE NOT NULL,
    password_hash VARCHAR(255),
    employee_id INTEGER,
    role INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    last_login_date TIMESTAMP,
    created_date TIMESTAMP DEFAULT NOW()
);
```

4. Tabloların oluşturulduğunu doğrulayın (`Table Editor` sekmesinden kontrol edebilirsiniz)

### Supabase API Bilgilerini Alma:
1. Supabase dashboard'da `Settings > API` sekmesine gidin
2. `Project URL` değerini kopyalayın (örn: `https://xxxxx.supabase.co`)
3. `anon` `public` key'i kopyalayın (API Keys bölümünden)

### Test Verileri Ekleme:
SQL Editor'de aşağıdaki kodu çalıştırın:

```sql
-- Test departmanı
INSERT INTO departments (name, description) 
VALUES ('IT', 'Bilgi Teknolojileri Departmanı')
ON CONFLICT DO NOTHING;

-- Test çalışanları
INSERT INTO employees (first_name, last_name, email, department_id, skills, max_workload) 
VALUES 
    ('Ahmet', 'Yılmaz', 'ahmet@test.com', 1, '["C#", "SQL", "DevExpress"]', 40),
    ('Ayşe', 'Demir', 'ayse@test.com', 1, '["Python", "AI", "Data Analysis"]', 40),
    ('Mehmet', 'Kaya', 'mehmet@test.com', 1, '["JavaScript", "React", "Node.js"]', 40)
ON CONFLICT DO NOTHING;

-- Test kullanıcıları (şifre: 123 - gerçek uygulamada hash'lenmiş olmalı)
INSERT INTO users (username, password_hash, employee_id, role) 
VALUES 
    ('manager', 'demo_hash_123', 1, 1),
    ('employee', 'demo_hash_123', 2, 0)
ON CONFLICT DO NOTHING;
```

## Adım 5: OpenAI API Anahtarı Alma

1. [https://platform.openai.com](https://platform.openai.com) adresine gidin
2. Hesap oluşturun veya giriş yapın
3. `API Keys` sekmesine gidin
4. `Create new secret key` butonuna tıklayın
5. Oluşturulan key'i kopyalayın (bir daha gösterilmeyecek!)

**Alternatif:** Gemini API kullanmak isterseniz:
1. [https://makersuite.google.com/app/apikey](https://makersuite.google.com/app/apikey) adresine gidin
2. API key oluşturun

## Adım 6: App.config Dosyasını Yapılandırma

1. Visual Studio'da `App.config` dosyasını açın
2. Aşağıdaki değerleri kendi bilgilerinizle değiştirin:

```xml
<appSettings>
    <!-- Supabase Ayarları -->
    <add key="SupabaseUrl" value="BURAYA_SUPABASE_URL_YAZIN" />
    <add key="SupabaseKey" value="BURAYA_SUPABASE_KEY_YAZIN" />
    
    <!-- OpenAI Ayarları -->
    <add key="OpenAIApiKey" value="BURAYA_OPENAI_KEY_YAZIN" />
    <add key="OpenAIUrl" value="https://api.openai.com" />
</appSettings>
```

**Örnek:**
```xml
<add key="SupabaseUrl" value="https://abcdefghijklmnop.supabase.co" />
<add key="SupabaseKey" value="eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..." />
<add key="OpenAIApiKey" value="sk-proj-xxxxxxxxxxxxxxxxxxxxx" />
```

## Adım 7: Windows Speech Recognition Ayarları

1. Windows Ayarlar > Gizlilik > Mikrofon
2. "Mikrofon erişimine izin ver" seçeneğinin açık olduğundan emin olun
3. "Uygulamaların mikrofonunuza erişmesine izin ver" seçeneğini açın

## Adım 8: Projeyi Derleme ve Çalıştırma

1. Visual Studio'da `Build > Build Solution` (Ctrl+Shift+B) ile projeyi derleyin
2. Hata varsa düzeltin (genellikle eksik referanslar olabilir)
3. `Debug > Start Debugging` (F5) ile projeyi çalıştırın

## Adım 9: İlk Giriş

1. Uygulama açıldığında login ekranı gelecek
2. Demo hesaplar:
   - **Yönetici:** `manager` / `123`
   - **Çalışan:** `employee` / `123`

## ⚠️ Önemli Notlar

### DevExpress Lisansı:
- DevExpress lisanslı bir üründür
- Eğitim amaçlı kullanım için trial sürümü kullanabilirsiniz
- Ticari kullanım için lisans satın almanız gerekir

### DevExpress Kontrollerini Ekleme:
Şu an proje standart Windows Forms kontrolleri ile çalışıyor. DevExpress kontrollerini eklemek için:

1. **ManagerDashboard.cs** dosyasını açın
2. `DataGridView` yerine `DevExpress.XtraGrid.GridControl` kullanın
3. Heatmap için `DevExpress.XtraCharts.ChartControl` ekleyin
4. Benzer şekilde diğer formlarda da DevExpress kontrollerini kullanın

### Supabase Row Level Security (RLS):
Supabase'de RLS politikalarını yapılandırmanız gerekebilir:

```sql
-- Tüm tablolar için RLS'yi etkinleştir
ALTER TABLE employees ENABLE ROW LEVEL SECURITY;
ALTER TABLE tasks ENABLE ROW LEVEL SECURITY;
ALTER TABLE departments ENABLE ROW LEVEL SECURITY;
ALTER TABLE meetings ENABLE ROW LEVEL SECURITY;

-- Herkesin okuyabilmesi için (geliştirme aşaması)
CREATE POLICY "Enable read access for all users" ON employees FOR SELECT USING (true);
CREATE POLICY "Enable read access for all users" ON tasks FOR SELECT USING (true);
CREATE POLICY "Enable read access for all users" ON departments FOR SELECT USING (true);
CREATE POLICY "Enable read access for all users" ON meetings FOR SELECT USING (true);

-- Herkesin yazabilmesi için (geliştirme aşaması)
CREATE POLICY "Enable insert access for all users" ON employees FOR INSERT WITH CHECK (true);
CREATE POLICY "Enable insert access for all users" ON tasks FOR INSERT WITH CHECK (true);
CREATE POLICY "Enable update access for all users" ON tasks FOR UPDATE USING (true);
```

## 🐛 Sorun Giderme

### "Newtonsoft.Json bulunamadı" hatası:
- NuGet Package Manager'dan paketi tekrar yükleyin
- `packages.config` dosyasının projede olduğundan emin olun

### "DevExpress kontrolleri bulunamadı" hatası:
- DevExpress'in düzgün kurulduğundan emin olun
- Visual Studio'yu yönetici olarak çalıştırıp `Register Controls` işlemini tekrar yapın

### "Supabase bağlantı hatası":
- URL ve API key'in doğru olduğundan emin olun
- Supabase projenizin aktif olduğundan emin olun
- RLS politikalarını kontrol edin

### "Ses tanıma çalışmıyor":
- Mikrofon izinlerini kontrol edin
- Windows Speech Recognition servisinin çalıştığından emin olun
- Sistem dilinin Türkçe olması önerilir

### "AI servisi yanıt vermiyor":
- API key'in geçerli olduğundan emin olun
- İnternet bağlantınızı kontrol edin
- OpenAI hesabınızda kredi olduğundan emin olun

## ✅ Kurulum Kontrol Listesi

- [ ] Visual Studio açıldı
- [ ] NuGet paketleri yüklendi (Newtonsoft.Json)
- [ ] DevExpress kuruldu ve kayıtlı
- [ ] Supabase hesabı oluşturuldu
- [ ] Supabase tabloları oluşturuldu
- [ ] Test verileri eklendi
- [ ] Supabase URL ve Key alındı
- [ ] OpenAI API key alındı
- [ ] App.config güncellendi
- [ ] Windows mikrofon izinleri verildi
- [ ] Proje derlendi (hata yok)
- [ ] Uygulama çalıştırıldı ve login ekranı göründü

## 🎉 Başarılı!

Artık projeniz hazır! Herhangi bir sorunla karşılaşırsanız yukarıdaki sorun giderme bölümüne bakın.

