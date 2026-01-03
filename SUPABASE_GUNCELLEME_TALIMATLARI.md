# 📋 Supabase Güncelleme Talimatları

## 🎯 Yapılacaklar

Projenin güncel haline göre Supabase veritabanında şu işlemleri yapmanız gerekiyor:

### 1. ✅ Tabloları Kontrol Et

Aşağıdaki tabloların mevcut olduğundan emin olun:
- `departments` (id, name, description, manager_id)
- `employees` (id, first_name, last_name, email, department_id, position, skills, current_workload, max_workload, is_active, created_date)
- `users` (id, username, password_hash, employee_id, role, is_active)
- `tasks` (id, title, description, assigned_to_id, created_by_id, created_date, due_date, status, priority, department_id, skills_required, estimated_hours, actual_hours, notes, completed_date)
- `meetings` (id, title, description, start_time, end_time, organizer_id, location, attendee_ids, is_reminder_sent)
- `task_comments` (id, task_id, user_id, user_name, comment_text, created_at)

### 2. 🔧 Eksik Kolonları Ekle (Eğer Yoksa)

#### `tasks` Tablosu:
```sql
-- Eğer bu kolonlar yoksa ekleyin:
ALTER TABLE tasks 
ADD COLUMN IF NOT EXISTS notes TEXT,
ADD COLUMN IF NOT EXISTS completed_date TIMESTAMP,
ADD COLUMN IF NOT EXISTS actual_hours INTEGER DEFAULT 0,
ADD COLUMN IF NOT EXISTS created_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP;

-- department_id nullable yap (zaten nullable olmalı ama kontrol edin)
ALTER TABLE tasks 
ALTER COLUMN department_id DROP NOT NULL;

-- estimated_hours nullable yap
ALTER TABLE tasks 
ALTER COLUMN estimated_hours DROP NOT NULL;
```

#### `employees` Tablosu:
```sql
-- Eğer created_date yoksa ekleyin:
ALTER TABLE employees 
ADD COLUMN IF NOT EXISTS created_date TIMESTAMP DEFAULT CURRENT_TIMESTAMP;
```

### 3. 📥 Test Verilerini Yükle

⚠️ **ÖNEMLİ UYARI**: Script çalıştırıldığında **TÜM ESKİ VERİLER SİLİNECEKTİR!** (TRUNCATE komutları aktif)

1. Supabase Dashboard'a giriş yapın
2. Sol menüden **"SQL Editor"** seçin
3. **"New Query"** butonuna tıklayın
4. `TEST_VERILERI_GUNCELLENMIS.sql` dosyasını açın
5. **TÜM İÇERİĞİNİ** kopyalayıp SQL Editor'e yapıştırın
6. **"Run"** butonuna tıklayın (veya F5)
7. Script otomatik olarak eski verileri temizleyip yeni verileri yükleyecektir

### 4. ✅ Verileri Kontrol Et

SQL Editor'de şu sorguları çalıştırarak verilerin doğru yüklendiğini kontrol edin:

```sql
-- Toplam kayıt sayıları
SELECT 'Departmanlar' as Tablo, COUNT(*) as KayitSayisi FROM departments
UNION ALL
SELECT 'Çalışanlar', COUNT(*) FROM employees
UNION ALL
SELECT 'Kullanıcılar', COUNT(*) FROM users
UNION ALL
SELECT 'Görevler', COUNT(*) FROM tasks
UNION ALL
SELECT 'Toplantılar', COUNT(*) FROM meetings;

-- Çalışanlar ve iş yükleri
SELECT 
    e.first_name || ' ' || e.last_name as Ad,
    d.name as Departman,
    e.current_workload || '/' || e.max_workload as IsYuku,
    ROUND((e.current_workload::numeric / NULLIF(e.max_workload, 0) * 100), 1) || '%' as Yuzde
FROM employees e
LEFT JOIN departments d ON e.department_id = d.id
ORDER BY e.current_workload DESC;

-- Görev durumları
SELECT 
    status,
    COUNT(*) as Adet
FROM tasks
GROUP BY status
ORDER BY status;

-- Gecikmiş görevler
SELECT 
    t.title,
    e.first_name || ' ' || e.last_name as Atanan,
    t.due_date,
    CURRENT_DATE - t.due_date::date as GecikmeGunu
FROM tasks t
LEFT JOIN employees e ON t.assigned_to_id = e.id
WHERE t.due_date < CURRENT_DATE 
  AND t.status != 2  -- Completed değil
ORDER BY t.due_date;
```

### 5. 🔐 Row Level Security (RLS) Ayarları

Eğer RLS aktifse, şu politikaları ekleyin:

```sql
-- Tasks tablosu için
ALTER TABLE tasks ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view all tasks" ON tasks
    FOR SELECT USING (true);

CREATE POLICY "Users can insert tasks" ON tasks
    FOR INSERT WITH CHECK (true);

CREATE POLICY "Users can update tasks" ON tasks
    FOR UPDATE USING (true);

CREATE POLICY "Users can delete tasks" ON tasks
    FOR DELETE USING (true);

-- Employees tablosu için
ALTER TABLE employees ENABLE ROW LEVEL SECURITY;

CREATE POLICY "Users can view all employees" ON employees
    FOR SELECT USING (true);
```

### 6. 📊 Beklenen Sonuçlar

Script çalıştıktan sonra:
- ✅ 4 departman
- ✅ 10 çalışan
- ✅ 7 kullanıcı (1 manager, 1 admin, 5 employee)
- ✅ 16 görev (2 gecikmiş, çeşitli durumlar)
- ✅ 5 toplantı

### 7. ⚠️ Önemli Notlar

1. **ESKİ VERİLER SİLİNECEK**: Script'in başında TRUNCATE komutları var, bu yüzden tüm mevcut veriler silinecek. Eğer mevcut verilerinizi korumak istiyorsanız, script'teki TRUNCATE satırlarını yorum satırı yapın.

2. **ID'ler**: Script'te ID'ler manuel belirtilmiş. Eğer tablolarınızda AUTO_INCREMENT varsa, ID'leri kaldırın ve Supabase'in otomatik ID üretmesine izin verin.

3. **Tarihler**: `CURRENT_DATE` ve `CURRENT_TIMESTAMP` kullanıldı, bu yüzden her çalıştırmada güncel tarihler kullanılacak.

4. **ON CONFLICT**: Script `ON CONFLICT DO UPDATE` kullanıyor, bu yüzden aynı ID'li kayıtlar güncellenecek.

5. **Null Değerler**: `department_id` ve `estimated_hours` artık nullable, bu yüzden bazı görevlerde null olabilir.

6. **Detaylı Açıklamalar**: Görev açıklamaları çok detaylı hazırlandı, AI'dan daha iyi sonuçlar almak için. Her görev için kapsamlı bilgi verildi.

---

## 🚀 Hızlı Başlangıç

1. Supabase Dashboard → SQL Editor
2. `TEST_VERILERI_GUNCELLENMIS.sql` dosyasını aç
3. Tüm içeriği kopyala-yapıştır
4. Run (F5)
5. ✅ Başarılı mesajını gör
6. Uygulamayı çalıştır ve test et!

