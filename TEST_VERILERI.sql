-- ============================================
-- TEST VERİLERİ - Supabase SQL Script
-- ============================================
-- Bu script'i Supabase SQL Editor'de çalıştırın

-- Önce mevcut verileri temizle (opsiyonel)
-- TRUNCATE TABLE tasks CASCADE;
-- TRUNCATE TABLE meetings CASCADE;
-- TRUNCATE TABLE employees CASCADE;
-- TRUNCATE TABLE departments CASCADE;
-- TRUNCATE TABLE users CASCADE;

-- ============================================
-- 1. DEPARTMANLAR
-- ============================================
INSERT INTO departments (name, description, manager_id) 
VALUES 
    ('Bilgi Teknolojileri', 'IT Departmanı - Yazılım Geliştirme ve Sistem Yönetimi', 1),
    ('İnsan Kaynakları', 'HR Departmanı - Personel Yönetimi', 2),
    ('Pazarlama', 'Marketing Departmanı - Dijital Pazarlama ve İletişim', 3),
    ('Satış', 'Sales Departmanı - Müşteri İlişkileri ve Satış', 4)
ON CONFLICT DO NOTHING;

-- ============================================
-- 2. ÇALIŞANLAR
-- ============================================
INSERT INTO employees (first_name, last_name, email, department_id, position, skills, current_workload, max_workload, is_active) 
VALUES 
    -- IT Departmanı
    ('Ahmet', 'Yılmaz', 'ahmet.yilmaz@ofis.com', 1, 'Yazılım Geliştirici', '["C#", "DevExpress", "SQL", "ASP.NET"]', 15, 40, true),
    ('Ayşe', 'Demir', 'ayse.demir@ofis.com', 1, 'AI Uzmanı', '["Python", "Machine Learning", "OpenAI API", "Data Analysis"]', 20, 40, true),
    ('Mehmet', 'Kaya', 'mehmet.kaya@ofis.com', 1, 'Full Stack Developer', '["JavaScript", "React", "Node.js", "MongoDB"]', 10, 40, true),
    ('Zeynep', 'Şahin', 'zeynep.sahin@ofis.com', 1, 'DevOps Engineer', '["Docker", "Kubernetes", "CI/CD", "AWS"]', 25, 40, true),
    
    -- HR Departmanı
    ('Fatma', 'Özkan', 'fatma.ozkan@ofis.com', 2, 'İK Uzmanı', '["İnsan Kaynakları", "İşe Alım", "Eğitim"]', 12, 40, true),
    ('Ali', 'Çelik', 'ali.celik@ofis.com', 2, 'İK Müdürü', '["Stratejik Planlama", "Performans Yönetimi"]', 18, 40, true),
    
    -- Pazarlama Departmanı
    ('Elif', 'Arslan', 'elif.arslan@ofis.com', 3, 'Dijital Pazarlama Uzmanı', '["SEO", "Google Ads", "Social Media", "Content Marketing"]', 22, 40, true),
    ('Can', 'Yıldız', 'can.yildiz@ofis.com', 3, 'Pazarlama Müdürü', '["Strateji", "Brand Management", "Analytics"]', 30, 40, true),
    
    -- Satış Departmanı
    ('Selin', 'Aydın', 'selin.aydin@ofis.com', 4, 'Satış Temsilcisi', '["Müşteri İlişkileri", "CRM", "Sunum"]', 8, 40, true),
    ('Burak', 'Koç', 'burak.koc@ofis.com', 4, 'Satış Müdürü', '["Satış Stratejisi", "Müşteri Yönetimi", "Raporlama"]', 35, 40, true)
ON CONFLICT DO NOTHING;

-- ============================================
-- 3. KULLANICILAR (Demo Hesaplar)
-- ============================================
INSERT INTO users (username, password_hash, employee_id, role, is_active) 
VALUES 
    -- Yönetici hesapları
    ('manager', 'demo_hash_123', 1, 1, true),  -- Ahmet Yılmaz - Manager
    ('admin', 'demo_hash_123', 4, 2, true),   -- Zeynep Şahin - Admin
    
    -- Çalışan hesapları
    ('employee1', 'demo_hash_123', 2, 0, true), -- Ayşe Demir
    ('employee2', 'demo_hash_123', 3, 0, true), -- Mehmet Kaya
    ('employee3', 'demo_hash_123', 5, 0, true), -- Fatma Özkan
    ('employee4', 'demo_hash_123', 7, 0, true), -- Elif Arslan
    ('employee5', 'demo_hash_123', 9, 0, true)  -- Selin Aydın
ON CONFLICT DO NOTHING;

-- ============================================
-- 4. GÖREVLER (Tasks)
-- ============================================
INSERT INTO tasks (title, description, assigned_to_id, created_by_id, due_date, status, priority, department_id, skills_required, estimated_hours) 
VALUES 
    -- IT Departmanı Görevleri
    ('Yeni Özellik: Sesli Komut Sistemi', 'Sesli komut sistemini geliştir ve entegre et. OpenAI API kullan.', 2, 1, CURRENT_DATE + INTERVAL '5 days', 1, 3, 1, '["C#", "OpenAI API", "Speech Recognition"]', 16),
    ('Veritabanı Optimizasyonu', 'Supabase veritabanı sorgularını optimize et ve index ekle.', 1, 1, CURRENT_DATE + INTERVAL '3 days', 1, 2, 1, '["SQL", "Database Optimization"]', 8),
    ('DevExpress Grid Entegrasyonu', 'ManagerDashboard formuna DevExpress GridControl ekle ve yapılandır.', 1, 1, CURRENT_DATE + INTERVAL '7 days', 0, 2, 1, '["C#", "DevExpress", "WinForms"]', 12),
    ('AI Öneri Algoritması İyileştirme', 'Personel atama öneri algoritmasını geliştir ve test et.', 2, 1, CURRENT_DATE + INTERVAL '10 days', 0, 1, 1, '["Python", "AI", "Algorithm"]', 20),
    ('Mobil Uygulama API Geliştirme', 'Mobil uygulama için REST API endpointleri oluştur.', 3, 1, CURRENT_DATE + INTERVAL '14 days', 0, 2, 1, '["Node.js", "REST API", "MongoDB"]', 24),
    
    -- HR Departmanı Görevleri
    ('Yeni İşe Alım Süreci', '2024 yılı için yeni işe alım sürecini planla ve uygula.', 5, 2, CURRENT_DATE + INTERVAL '6 days', 1, 2, 2, '["İnsan Kaynakları", "Planlama"]', 10),
    ('Çalışan Performans Değerlendirmesi', 'Q4 performans değerlendirmelerini tamamla ve raporla.', 6, 2, CURRENT_DATE + INTERVAL '4 days', 1, 3, 2, '["Performans Yönetimi", "Raporlama"]', 12),
    ('Eğitim Programı Hazırlama', 'Yeni çalışanlar için oryantasyon programı hazırla.', 5, 2, CURRENT_DATE + INTERVAL '8 days', 0, 1, 2, '["Eğitim", "İçerik Geliştirme"]', 16),
    
    -- Pazarlama Departmanı Görevleri
    ('Sosyal Medya Kampanyası', 'Yeni ürün lansmanı için sosyal medya kampanyası hazırla.', 7, 3, CURRENT_DATE + INTERVAL '5 days', 1, 3, 3, '["Social Media", "Content Creation", "Marketing"]', 14),
    ('SEO Optimizasyonu', 'Web sitesi için SEO optimizasyonu yap ve raporla.', 7, 3, CURRENT_DATE + INTERVAL '7 days', 0, 2, 3, '["SEO", "Analytics", "Web"]', 18),
    ('Google Ads Kampanyası', 'Yeni Google Ads kampanyası oluştur ve yönet.', 8, 3, CURRENT_DATE + INTERVAL '3 days', 1, 2, 3, '["Google Ads", "PPC", "Analytics"]', 10),
    
    -- Satış Departmanı Görevleri
    ('Müşteri Sunumu Hazırlama', 'Yeni müşteri için ürün sunumu hazırla ve sun.', 9, 4, CURRENT_DATE + INTERVAL '2 days', 1, 3, 4, '["Sunum", "Müşteri İlişkileri", "CRM"]', 6),
    ('Aylık Satış Raporu', 'Ocak ayı satış raporunu hazırla ve yönetime sun.', 10, 4, CURRENT_DATE + INTERVAL '1 days', 1, 3, 4, '["Raporlama", "Analytics", "Excel"]', 4),
    ('Yeni Müşteri Ziyareti', 'Potansiyel yeni müşteriyi ziyaret et ve teklif sun.', 9, 4, CURRENT_DATE + INTERVAL '4 days', 0, 2, 4, '["Müşteri İlişkileri", "Satış"]', 8),
    
    -- Gecikmiş Görevler (Test için)
    ('Eski Proje: Sistem Migrasyonu', 'Eski sistemi yeni sisteme taşı. URGENT!', 1, 1, CURRENT_DATE - INTERVAL '5 days', 1, 3, 1, '["Migration", "System Administration"]', 40),
    ('Ertelenen: Dokümantasyon', 'Proje dokümantasyonunu tamamla.', 3, 1, CURRENT_DATE - INTERVAL '3 days', 0, 1, 1, '["Documentation", "Technical Writing"]', 12)
ON CONFLICT DO NOTHING;

-- ============================================
-- 5. TOPLANTILAR (Meetings)
-- ============================================
INSERT INTO meetings (title, description, start_time, end_time, organizer_id, location, attendee_ids, is_reminder_sent) 
VALUES 
    -- Bugünkü Toplantılar
    ('Günlük Scrum Toplantısı', 'Günlük sprint durumu ve blokajlar', 
     CURRENT_DATE + INTERVAL '9 hours', 
     CURRENT_DATE + INTERVAL '9 hours 30 minutes', 
     1, 'Toplantı Odası A', '[1,2,3]', false),
    
    ('Proje Planlama Toplantısı', 'Yeni özellikler için planlama', 
     CURRENT_DATE + INTERVAL '14 hours', 
     CURRENT_DATE + INTERVAL '15 hours 30 minutes', 
     1, 'Toplantı Odası B', '[1,2,4,8]', false),
    
    ('Departman Toplantısı - HR', 'Aylık departman toplantısı', 
     CURRENT_DATE + INTERVAL '10 hours', 
     CURRENT_DATE + INTERVAL '11 hours', 
     6, 'Toplantı Odası C', '[5,6]', false),
    
    -- Yarınki Toplantılar
    ('Müşteri Sunumu', 'Yeni müşteriye ürün sunumu', 
     CURRENT_DATE + INTERVAL '1 day' + INTERVAL '13 hours', 
     CURRENT_DATE + INTERVAL '1 day' + INTERVAL '14 hours 30 minutes', 
     9, 'Müşteri Ofisi', '[9,10]', false),
    
    -- Gelecek Hafta
    ('Strateji Toplantısı', 'Q2 strateji planlaması', 
     CURRENT_DATE + INTERVAL '7 days' + INTERVAL '10 hours', 
     CURRENT_DATE + INTERVAL '7 days' + INTERVAL '12 hours', 
     4, 'Yönetim Toplantı Salonu', '[1,4,6,8,10]', false)
ON CONFLICT DO NOTHING;

-- ============================================
-- VERİ KONTROLÜ
-- ============================================
-- Verilerin doğru eklendiğini kontrol et:
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
    ROUND((e.current_workload::numeric / e.max_workload::numeric * 100), 1) || '%' as Yuzde
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

