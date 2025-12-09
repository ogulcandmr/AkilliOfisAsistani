# AI Destekli Ofis Görev Otomasyonu

Bu proje, C# ve DevExpress kullanılarak geliştirilmiş kapsamlı bir masaüstü otomasyon sistemidir.

## 🚀 Özellikler

### 1. 🎤 Sesli Yönetici Modülü
- Sesli görev atama (Voice-to-Task)
- Sesli rapor sorgulama
- Mikrofon ile komut verme

### 2. 👨‍💼 Yönetici Paneli
- AI destekli personel atama önerileri
- Canlı iş yükü ve performans takibi
- Isı haritası ile görselleştirme
- Anomali tespiti (gecikmiş görevler, aşırı iş yükü)

### 3. 👩‍💻 Çalışan Paneli
- Günlük akıllı brifing
- Kanban panosu (sürükle-bırak)
- AI alt görev sihirbazı

### 4. 🔔 Akıllı Bildirimler
- Proaktif deadline uyarıları
- Toplantı hatırlatmaları

## 📋 Kurulum Adımları

### 1. Gereksinimler
- Visual Studio 2019 veya üzeri
- .NET Framework 4.8
- DevExpress (lisanslı)
- Supabase hesabı
- OpenAI API anahtarı (veya Gemini API)

### 2. NuGet Paketleri
Projeyi açtıktan sonra NuGet Package Manager'dan şu paketi yükleyin:
- Newtonsoft.Json (13.0.3)

### 3. DevExpress Kurulumu
1. DevExpress'i bilgisayarınıza kurun
2. Visual Studio'da Tools > DevExpress > Register Controls ile kontrolleri kaydedin
3. Toolbox'a DevExpress kontrollerini ekleyin

### 4. Supabase Kurulumu
1. [Supabase](https://supabase.com) hesabı oluşturun
2. Yeni bir proje oluşturun
3. SQL Editor'de aşağıdaki tabloları oluşturun:

```sql
-- Employees tablosu
CREATE TABLE employees (
    id SERIAL PRIMARY KEY,
    first_name VARCHAR(100),
    last_name VARCHAR(100),
    email VARCHAR(255),
    department_id INTEGER,
    position VARCHAR(100),
    skills TEXT,
    current_workload INTEGER DEFAULT 0,
    max_workload INTEGER DEFAULT 40,
    is_active BOOLEAN DEFAULT true,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Departments tablosu
CREATE TABLE departments (
    id SERIAL PRIMARY KEY,
    name VARCHAR(100),
    description TEXT,
    manager_id INTEGER,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Tasks tablosu
CREATE TABLE tasks (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255),
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
CREATE TABLE meetings (
    id SERIAL PRIMARY KEY,
    title VARCHAR(255),
    description TEXT,
    start_time TIMESTAMP,
    end_time TIMESTAMP,
    organizer_id INTEGER,
    location VARCHAR(255),
    attendee_ids TEXT,
    is_reminder_sent BOOLEAN DEFAULT false,
    created_date TIMESTAMP DEFAULT NOW()
);

-- Users tablosu
CREATE TABLE users (
    id SERIAL PRIMARY KEY,
    username VARCHAR(100) UNIQUE,
    password_hash VARCHAR(255),
    employee_id INTEGER,
    role INTEGER DEFAULT 0,
    is_active BOOLEAN DEFAULT true,
    last_login_date TIMESTAMP,
    created_date TIMESTAMP DEFAULT NOW()
);
```

4. Supabase proje URL'inizi ve anon key'inizi kopyalayın

### 5. API Anahtarlarını Yapılandırma
`App.config` dosyasını açın ve şu değerleri güncelleyin:

```xml
<appSettings>
    <add key="SupabaseUrl" value="https://your-project-id.supabase.co" />
    <add key="SupabaseKey" value="your-supabase-anon-key" />
    <add key="OpenAIApiKey" value="your-openai-api-key" />
    <add key="OpenAIUrl" value="https://api.openai.com" />
</appSettings>
```

### 6. DevExpress Kontrollerini Ekleme
Şu an proje standart Windows Forms kontrolleri ile çalışıyor. DevExpress kontrollerini eklemek için:

1. **ManagerDashboard.cs** içinde:
   - `DataGridView` yerine `DevExpress.XtraGrid.GridControl` kullanın
   - Heatmap için `DevExpress.XtraCharts.ChartControl` ekleyin
   - Grafikler için `DevExpress.XtraCharts` kullanın

2. **EmployeeWorkspace.cs** içinde:
   - Kanban için `DevExpress.XtraEditors.TileView` veya özel bir Kanban kontrolü kullanın

3. **Ana Form** içinde:
   - Ribbon için `DevExpress.XtraBars.Ribbon.RibbonControl` ekleyin

### 7. Test Verileri Ekleme
Supabase'de test verileri ekleyin:

```sql
-- Test departmanı
INSERT INTO departments (name, description) VALUES ('IT', 'Bilgi Teknolojileri');

-- Test çalışanları
INSERT INTO employees (first_name, last_name, email, department_id, skills, max_workload) 
VALUES 
    ('Ahmet', 'Yılmaz', 'ahmet@test.com', 1, '["C#", "SQL", "DevExpress"]', 40),
    ('Ayşe', 'Demir', 'ayse@test.com', 1, '["Python", "AI", "Data Analysis"]', 40);

-- Test kullanıcıları
INSERT INTO users (username, password_hash, employee_id, role) 
VALUES 
    ('manager', 'hashed_password', 1, 1),
    ('employee', 'hashed_password', 2, 0);
```

## 🎯 Kullanım

1. Projeyi Visual Studio'da açın
2. `F5` ile çalıştırın
3. Demo giriş bilgileri:
   - Yönetici: `manager` / `123`
   - Çalışan: `employee` / `123`

## 📝 Notlar

- Sesli komutlar için Windows Speech Recognition servisinin çalışıyor olması gerekir
- AI özellikleri için internet bağlantısı gereklidir
- DevExpress kontrolleri lisanslı olmalıdır
- Supabase Row Level Security (RLS) ayarlarını yapılandırmanız gerekebilir

## 🔧 Sorun Giderme

### Ses tanıma çalışmıyor
- Windows Ayarlar > Gizlilik > Mikrofon izinlerini kontrol edin
- Sistem dilinin Türkçe olması gerekebilir

### Supabase bağlantı hatası
- URL ve API key'in doğru olduğundan emin olun
- CORS ayarlarını kontrol edin
- RLS politikalarını kontrol edin

### AI servisi yanıt vermiyor
- API key'in geçerli olduğundan emin olun
- İnternet bağlantınızı kontrol edin
- API quota limitinizi kontrol edin

## 📞 Destek

Sorularınız için proje sahibi ile iletişime geçin.

