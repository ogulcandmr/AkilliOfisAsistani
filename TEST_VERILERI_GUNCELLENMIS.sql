-- ============================================
-- GÜNCELLENMİŞ TEST VERİLERİ - Supabase SQL Script
-- ============================================
-- Bu script'i Supabase SQL Editor'de çalıştırın
-- ⚠️ DİKKAT: Bu script ESKİ VERİLERİ SİLECEKTİR!
-- ============================================

-- ESKİ VERİLERİ TEMİZLE (ÖNCE BUNLAR ÇALIŞACAK)
TRUNCATE TABLE task_comments CASCADE;
TRUNCATE TABLE tasks CASCADE;
TRUNCATE TABLE meetings CASCADE;
TRUNCATE TABLE employees CASCADE;
TRUNCATE TABLE departments CASCADE;
TRUNCATE TABLE users CASCADE;

-- ============================================
-- 1. DEPARTMANLAR
-- ============================================
INSERT INTO departments (id, name, description, manager_id) 
VALUES 
    (1, 'Bilgi Teknolojileri', 'IT Departmanı - Yazılım Geliştirme ve Sistem Yönetimi', 1),
    (2, 'İnsan Kaynakları', 'HR Departmanı - Personel Yönetimi', 2),
    (3, 'Pazarlama', 'Marketing Departmanı - Dijital Pazarlama ve İletişim', 3),
    (4, 'Satış', 'Sales Departmanı - Müşteri İlişkileri ve Satış', 4)
ON CONFLICT (id) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    manager_id = EXCLUDED.manager_id;

-- ============================================
-- 2. ÇALIŞANLAR (GÜNCELLENMİŞ)
-- ============================================
INSERT INTO employees (id, first_name, last_name, email, department_id, position, skills, current_workload, max_workload, is_active, created_date) 
VALUES 
    -- IT Departmanı
    (1, 'Ahmet', 'Yılmaz', 'ahmet.yilmaz@ofis.com', 1, 'Yazılım Geliştirici', '["C#", "DevExpress", "SQL", "ASP.NET", "WinForms"]', 15, 40, true, CURRENT_TIMESTAMP),
    (2, 'Ayşe', 'Demir', 'ayse.demir@ofis.com', 1, 'AI Uzmanı', '["Python", "Machine Learning", "OpenAI API", "Data Analysis", "AI/ML"]', 20, 40, true, CURRENT_TIMESTAMP),
    (3, 'Mehmet', 'Kaya', 'mehmet.kaya@ofis.com', 1, 'Full Stack Developer', '["JavaScript", "React", "Node.js", "MongoDB", "REST API"]', 10, 40, true, CURRENT_TIMESTAMP),
    (4, 'Zeynep', 'Şahin', 'zeynep.sahin@ofis.com', 1, 'DevOps Engineer', '["Docker", "Kubernetes", "CI/CD", "AWS", "Linux"]', 25, 40, true, CURRENT_TIMESTAMP),
    
    -- HR Departmanı
    (5, 'Fatma', 'Özkan', 'fatma.ozkan@ofis.com', 2, 'İK Uzmanı', '["İnsan Kaynakları", "İşe Alım", "Eğitim", "Performans Yönetimi"]', 12, 40, true, CURRENT_TIMESTAMP),
    (6, 'Ali', 'Çelik', 'ali.celik@ofis.com', 2, 'İK Müdürü', '["Stratejik Planlama", "Performans Yönetimi", "Liderlik"]', 18, 40, true, CURRENT_TIMESTAMP),
    
    -- Pazarlama Departmanı
    (7, 'Elif', 'Arslan', 'elif.arslan@ofis.com', 3, 'Dijital Pazarlama Uzmanı', '["SEO", "Google Ads", "Social Media", "Content Marketing", "Analytics"]', 22, 40, true, CURRENT_TIMESTAMP),
    (8, 'Can', 'Yıldız', 'can.yildiz@ofis.com', 3, 'Pazarlama Müdürü', '["Strateji", "Brand Management", "Analytics", "Liderlik"]', 30, 40, true, CURRENT_TIMESTAMP),
    
    -- Satış Departmanı
    (9, 'Selin', 'Aydın', 'selin.aydin@ofis.com', 4, 'Satış Temsilcisi', '["Müşteri İlişkileri", "CRM", "Sunum", "Satış"]', 8, 40, true, CURRENT_TIMESTAMP),
    (10, 'Burak', 'Koç', 'burak.koc@ofis.com', 4, 'Satış Müdürü', '["Satış Stratejisi", "Müşteri Yönetimi", "Raporlama", "Liderlik"]', 35, 40, true, CURRENT_TIMESTAMP)
ON CONFLICT (id) DO UPDATE SET
    first_name = EXCLUDED.first_name,
    last_name = EXCLUDED.last_name,
    email = EXCLUDED.email,
    department_id = EXCLUDED.department_id,
    position = EXCLUDED.position,
    skills = EXCLUDED.skills,
    current_workload = EXCLUDED.current_workload,
    max_workload = EXCLUDED.max_workload,
    is_active = EXCLUDED.is_active;

-- ============================================
-- 3. KULLANICILAR (Demo Hesaplar)
-- ============================================
INSERT INTO users (id, username, password_hash, employee_id, role, is_active) 
VALUES 
    -- Yönetici hesapları
    (1, 'manager', 'demo_hash_123', 1, 1, true),  -- Ahmet Yılmaz - Manager
    (2, 'admin', 'demo_hash_123', 4, 2, true),   -- Zeynep Şahin - Admin
    
    -- Çalışan hesapları
    (3, 'employee1', 'demo_hash_123', 2, 0, true), -- Ayşe Demir
    (4, 'employee2', 'demo_hash_123', 3, 0, true), -- Mehmet Kaya
    (5, 'employee3', 'demo_hash_123', 5, 0, true), -- Fatma Özkan
    (6, 'employee4', 'demo_hash_123', 7, 0, true), -- Elif Arslan
    (7, 'employee5', 'demo_hash_123', 9, 0, true)  -- Selin Aydın
ON CONFLICT (id) DO UPDATE SET
    username = EXCLUDED.username,
    password_hash = EXCLUDED.password_hash,
    employee_id = EXCLUDED.employee_id,
    role = EXCLUDED.role,
    is_active = EXCLUDED.is_active;

-- ============================================
-- 4. GÖREVLER (Tasks) - GÜNCELLENMİŞ
-- ============================================
INSERT INTO tasks (id, title, description, assigned_to_id, created_by_id, created_date, due_date, status, priority, department_id, skills_required, estimated_hours, actual_hours, notes) 
VALUES 
    -- IT Departmanı Görevleri
    (1, 'Yeni Özellik: Sesli Komut Sistemi', 
     'Bu görev, ofis asistanı uygulamasına sesli komut özelliği eklemeyi içermektedir. Görev kapsamında: 1) OpenAI Whisper API entegrasyonu ile ses tanıma modülü geliştirilecek, 2) Kullanıcı sesli komutlarını (örneğin "Yeni görev oluştur", "Görevlerimi listele", "AI ile konuş") işleyecek bir komut parser yazılacak, 3) C# WinForms uygulamasına ses kayıt butonu ve sesli geri bildirim mekanizması eklenecek, 4) Kullanıcı deneyimini optimize etmek için hata yönetimi ve kullanıcı rehberliği sağlanacak, 5) Performans testleri yapılacak ve API rate limit yönetimi implemente edilecek. Bu özellik, özellikle elleri meşgul olan kullanıcılar için büyük bir avantaj sağlayacak ve uygulamanın erişilebilirliğini artıracaktır.', 
     2, 1, CURRENT_TIMESTAMP - INTERVAL '3 days', CURRENT_DATE + INTERVAL '5 days', 1, 3, 1, '["C#", "OpenAI API", "Speech Recognition", "Whisper API", "WinForms", "Audio Processing"]', 16, 0, 'Kritik özellik, öncelikli'),
     
    (2, 'Veritabanı Optimizasyonu', 
     'Supabase PostgreSQL veritabanında performans sorunları tespit edildi. Bu görev kapsamında: 1) Yavaş çalışan sorguları analiz etmek için EXPLAIN ANALYZE komutları kullanılacak, 2) Sık kullanılan sorgular için uygun indexler eklenecek (tasks tablosunda assigned_to_id, status, due_date kolonları için composite index), 3) N+1 query problemlerini çözmek için JOIN optimizasyonları yapılacak, 4) Connection pooling ayarları gözden geçirilecek, 5) Query cache mekanizması implemente edilecek, 6) Performans metrikleri toplanacak ve raporlanacak. Hedef: Sorgu sürelerini %60 azaltmak ve veritabanı yükünü optimize etmek.', 
     1, 1, CURRENT_TIMESTAMP - INTERVAL '5 days', CURRENT_DATE + INTERVAL '3 days', 1, 2, 1, '["SQL", "PostgreSQL", "Database Optimization", "Performance Tuning", "Indexing", "Query Analysis"]', 8, 4, 'Devam ediyor'),
     
    (3, 'DevExpress Grid Entegrasyonu', 
     'ManagerDashboard formuna profesyonel bir veri görüntüleme arayüzü eklemek için DevExpress GridControl entegrasyonu yapılacak. Görev detayları: 1) GridControl bileşenini form tasarımına ekleme ve veri bağlama, 2) Kolon yapılandırması (görev başlığı, atanan kişi, durum, öncelik, teslim tarihi), 3) Gelişmiş filtreleme özellikleri (metin arama, tarih aralığı, durum filtresi), 4) Sıralama ve gruplama özellikleri, 5) Satır seçimi ve çift tıklama ile görev detay formunu açma, 6) Özel renklendirme (gecikmiş görevler kırmızı, yüksek öncelikli görevler turuncu), 7) Export özelliği (Excel, PDF), 8) Responsive tasarım ve performans optimizasyonu. Bu entegrasyon, yöneticilerin görevleri daha etkili yönetmesini sağlayacak.', 
     1, 1, CURRENT_TIMESTAMP - INTERVAL '2 days', CURRENT_DATE + INTERVAL '7 days', 0, 2, 1, '["C#", "DevExpress", "WinForms", "GridControl", "Data Binding", "UI/UX"]', 12, 0, NULL),
     
    (4, 'AI Öneri Algoritması İyileştirme', 
     'Mevcut personel atama öneri algoritması basit kurallara dayanıyor. Bu görev, algoritmayı makine öğrenmesi tabanlı bir sisteme dönüştürmeyi hedefliyor. Yapılacaklar: 1) Geçmiş görev atama verilerini analiz etmek ve başarılı atamaları belirlemek, 2) Özellik mühendisliği (yetenek uyumu skoru, iş yükü skoru, departman uyumu, geçmiş performans), 3) Python ile scikit-learn kullanarak bir sınıflandırma modeli eğitmek, 4) Model performansını değerlendirmek (precision, recall, F1-score), 5) Modeli C# uygulamasına entegre etmek (ONNX veya REST API), 6) A/B testi yaparak yeni modelin performansını karşılaştırmak, 7) Model monitoring ve sürekli öğrenme mekanizması kurmak. Hedef: Atama başarı oranını %85''ten %95''e çıkarmak.', 
     2, 1, CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_DATE + INTERVAL '10 days', 0, 1, 1, '["Python", "Machine Learning", "scikit-learn", "AI", "Algorithm", "Data Science", "ONNX", "Model Training"]', 20, 0, NULL),
     
    (5, 'Mobil Uygulama API Geliştirme', 
     'Şirket mobil uygulaması için RESTful API geliştirilecek. Görev kapsamı: 1) Node.js ve Express.js kullanarak REST API server kurulumu, 2) MongoDB veritabanı entegrasyonu ve şema tasarımı, 3) Authentication ve authorization (JWT token tabanlı), 4) API endpoint''leri (GET /tasks, POST /tasks, PUT /tasks/:id, DELETE /tasks/:id, GET /employees, GET /dashboard/stats), 5) Request validation ve error handling, 6) Rate limiting ve güvenlik önlemleri, 7) API dokümantasyonu (Swagger/OpenAPI), 8) Unit testler ve integration testler, 9) API versioning stratejisi, 10) Deployment ve CI/CD pipeline kurulumu. API, RESTful prensiplere uygun olacak ve JSON formatında veri alışverişi yapacak.', 
     3, 1, CURRENT_TIMESTAMP - INTERVAL '4 days', CURRENT_DATE + INTERVAL '14 days', 0, 2, 1, '["Node.js", "Express.js", "REST API", "MongoDB", "JWT", "Swagger", "Testing", "CI/CD"]', 24, 0, NULL),
    
    -- HR Departmanı Görevleri
    (6, 'Yeni İşe Alım Süreci', 
     '2024 yılı için kapsamlı bir işe alım süreci tasarlanacak ve uygulanacak. Süreç şu adımları içeriyor: 1) İşe alım ihtiyaçlarını belirlemek için departman yöneticileriyle görüşmeler yapmak, 2) İş ilanlarını hazırlamak ve uygun platformlarda yayınlamak (LinkedIn, kariyer.net, şirket web sitesi), 3) Aday değerlendirme kriterleri oluşturmak (teknik yeterlilik, kültürel uyum, deneyim seviyesi), 4) CV tarama ve ön eleme sürecini otomatikleştirmek, 5) Mülakat sürecini planlamak (telefon görüşmesi, teknik mülakat, kültürel uyum mülakatı), 6) Aday takip sistemi kurmak, 7) Teklif süreci ve pazarlık stratejisi belirlemek, 8) Onboarding sürecini hazırlamak. Bu süreç, şirketin büyüme hedeflerine ulaşması için kritik öneme sahiptir.', 
     5, 2, CURRENT_TIMESTAMP - INTERVAL '2 days', CURRENT_DATE + INTERVAL '6 days', 1, 2, 2, '["İnsan Kaynakları", "Planlama", "İşe Alım", "Recruitment", "Talent Acquisition", "HR Strategy"]', 10, 5, 'Planlama aşamasında'),
     
    (7, 'Çalışan Performans Değerlendirmesi', 
     'Q4 2023 dönemi için tüm çalışanların performans değerlendirmelerini tamamlamak gerekiyor. Görev kapsamı: 1) Her çalışan için performans metriklerini toplamak (görev tamamlama oranı, zamanında teslim oranı, kalite skorları), 2) 360 derece geri bildirim toplamak (yönetici, eş düzey, alt düzey değerlendirmeleri), 3) Hedefler ve gerçekleşmeleri karşılaştırmak, 4) Güçlü yönler ve gelişim alanlarını belirlemek, 5) Detaylı performans raporları hazırlamak (her çalışan için özelleştirilmiş), 6) Yönetim kuruluna sunum hazırlamak (genel istatistikler, trendler, öneriler), 7) Çalışanlarla birebir görüşmeler yapmak ve geri bildirim vermek, 8) 2024 yılı için yeni hedefler belirlemek. Bu değerlendirme, terfi ve maaş artışı kararları için temel oluşturacak.', 
     6, 2, CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_DATE + INTERVAL '4 days', 1, 3, 2, '["Performans Yönetimi", "Raporlama", "Analytics", "360 Feedback", "Performance Review", "HR Analytics"]', 12, 8, 'Raporlama aşamasında'),
     
    (8, 'Eğitim Programı Hazırlama', 
     'Yeni işe alınan çalışanlar için kapsamlı bir oryantasyon ve eğitim programı hazırlanacak. Program içeriği: 1) Şirket kültürü ve değerleri sunumu, 2) Organizasyon yapısı ve departman tanıtımları, 3) İş süreçleri ve prosedürler eğitimi, 4) Kullanılan araçlar ve sistemler eğitimi (ofis asistanı uygulaması, CRM, proje yönetim araçları), 5) Güvenlik ve uyumluluk eğitimleri, 6) Mentor atama sistemi kurmak, 7) Eğitim materyalleri hazırlamak (sunumlar, videolar, dokümantasyon), 8) Eğitim takvimi oluşturmak ve kayıt sistemi kurmak, 9) Eğitim etkinliğini ölçmek için değerlendirme anketleri hazırlamak, 10) Sürekli iyileştirme mekanizması kurmak. Bu program, yeni çalışanların hızlıca verimli hale gelmesini sağlayacak.', 
     5, 2, CURRENT_TIMESTAMP, CURRENT_DATE + INTERVAL '8 days', 0, 1, 2, '["Eğitim", "İçerik Geliştirme", "Oryantasyon", "Onboarding", "Training", "Employee Development"]', 16, 0, NULL),
    
    -- Pazarlama Departmanı Görevleri
    (9, 'Sosyal Medya Kampanyası', 
     'Yeni ürün lansmanı için çok kanallı bir sosyal medya kampanyası tasarlanacak ve yürütülecek. Kampanya detayları: 1) Hedef kitle analizi ve persona oluşturma, 2) Platform seçimi (LinkedIn, Twitter, Instagram, Facebook) ve platform-spesifik stratejiler, 3) İçerik planı hazırlama (30 günlük takvim, günlük 2-3 post), 4) Görsel tasarımlar hazırlama (infografikler, videolar, carousel postlar), 5) İçerik yazımı (ürün özellikleri, kullanım senaryoları, müşteri hikayeleri), 6) Influencer işbirliği planlaması, 7) Hashtag stratejisi ve trend takibi, 8) Engagement stratejisi (yorumlara cevap verme, soruları yanıtlama), 9) Kampanya takibi ve analitik (reach, engagement rate, click-through rate), 10) A/B testleri yaparak en iyi performans gösteren içerikleri belirleme. Hedef: 50K+ reach ve %5 engagement rate.', 
     7, 3, CURRENT_TIMESTAMP - INTERVAL '3 days', CURRENT_DATE + INTERVAL '5 days', 1, 3, 3, '["Social Media", "Content Creation", "Marketing", "Campaign", "Digital Marketing", "Content Strategy", "Analytics"]', 14, 7, 'İçerik hazırlanıyor'),
     
    (10, 'SEO Optimizasyonu', 
     'Şirket web sitesinin arama motoru görünürlüğünü artırmak için kapsamlı bir SEO çalışması yapılacak. Çalışma kapsamı: 1) Mevcut SEO durumunu analiz etmek (Google Search Console, Ahrefs, SEMrush), 2) Anahtar kelime araştırması yapmak (long-tail keywords, rakip analizi, search volume analizi), 3) On-page SEO optimizasyonu (meta tags, heading yapısı, URL yapısı, internal linking), 4) Technical SEO (sayfa hızı optimizasyonu, mobile-friendliness, schema markup), 5) İçerik optimizasyonu (mevcut sayfaları güncelleme, yeni blog yazıları ekleme), 6) Backlink stratejisi (guest posting, directory submissions, broken link building), 7) Local SEO optimizasyonu (Google My Business, local citations), 8) SEO raporu hazırlama (keyword rankings, organic traffic, conversion rate). Hedef: Organik trafiği 3 ay içinde %40 artırmak.', 
     7, 3, CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_DATE + INTERVAL '7 days', 0, 2, 3, '["SEO", "Analytics", "Web", "Keywords", "Search Engine Optimization", "Content Marketing", "Technical SEO"]', 18, 0, NULL),
     
    (11, 'Google Ads Kampanyası', 
     'Yeni bir Google Ads kampanyası oluşturulacak ve optimize edilecek. Kampanya yönetimi: 1) Kampanya hedeflerini belirlemek (lead generation, brand awareness, sales), 2) Anahtar kelime araştırması ve keyword listesi oluşturma, 3) Kampanya yapısını tasarlamak (Search, Display, Shopping kampanyaları), 4) Reklam grupları oluşturma ve hedefleme ayarları, 5) Reklam metinleri yazma (headlines, descriptions, extensions), 6) Landing page optimizasyonu (conversion rate artırma), 7) Bütçe yönetimi ve teklif stratejisi (CPC, CPA optimizasyonu), 8) A/B testleri yaparak en iyi performans gösteren reklamları belirleme, 9) Negatif keyword listesi oluşturma, 10) Kampanya performansını takip etme ve raporlama (impressions, clicks, CTR, conversions, ROAS). Hedef: %3+ CTR ve $50 altında CPA.', 
     8, 3, CURRENT_TIMESTAMP - INTERVAL '2 days', CURRENT_DATE + INTERVAL '3 days', 1, 2, 3, '["Google Ads", "PPC", "Analytics", "Budget", "Paid Advertising", "Campaign Management", "Conversion Optimization"]', 10, 3, 'Kampanya aktif'),
    
    -- Satış Departmanı Görevleri
    (12, 'Müşteri Sunumu Hazırlama', 
     'Büyük bir potansiyel müşteriye yapılacak ürün sunumu için kapsamlı hazırlık yapılacak. Sunum hazırlığı: 1) Müşteri ihtiyaç analizi yapmak (ön görüşme notları, web sitesi analizi, LinkedIn profili inceleme), 2) Özelleştirilmiş sunum içeriği hazırlamak (müşterinin ihtiyaçlarına odaklı özellikler, use case''ler, ROI hesaplamaları), 3) Görsel materyaller hazırlamak (PowerPoint sunumu, demo videolar, infografikler), 4) Canlı demo hazırlığı (test verileri, senaryolar, olası sorular için cevaplar), 5) Fiyatlandırma teklifi hazırlamak (tier seçenekleri, özel paketler), 6) Rakiplerle karşılaştırma tablosu hazırlama, 7) Müşteri itirazlarına hazırlık (fiyat, güvenlik, özellikler), 8) Sunum provası yapmak, 9) Sunum sonrası takip planı hazırlamak. Bu sunum, büyük bir sözleşme kazanmak için kritik öneme sahiptir.', 
     9, 4, CURRENT_TIMESTAMP - INTERVAL '1 day', CURRENT_DATE + INTERVAL '2 days', 1, 3, 4, '["Sunum", "Müşteri İlişkileri", "CRM", "Demo", "Sales Presentation", "Proposal", "Client Relations"]', 6, 4, 'Sunum hazır'),
     
    (13, 'Aylık Satış Raporu', 
     'Ocak 2024 ayı için detaylı satış raporu hazırlanacak ve yönetim kuruluna sunulacak. Rapor içeriği: 1) Satış metrikleri toplama (toplam gelir, yeni müşteri sayısı, müşteri kaybı, ortalama işlem değeri), 2) Departman ve bireysel performans analizi, 3) Ürün/hizmet bazlı satış dağılımı, 4) Bölgesel satış analizi, 5) Satış kanalları performansı (doğrudan satış, online, ortaklıklar), 6) Müşteri segmentasyonu analizi, 7) Trend analizi (önceki aylarla karşılaştırma, yıllık büyüme), 8) Hedef vs gerçekleşen karşılaştırması, 9) Sorun alanlarını belirleme ve çözüm önerileri, 10) Şubat ayı için hedefler ve stratejiler. Rapor, Excel ve PowerPoint formatında hazırlanacak ve görsel grafikler içerecek.', 
     10, 4, CURRENT_TIMESTAMP, CURRENT_DATE + INTERVAL '1 day', 1, 3, 4, '["Raporlama", "Analytics", "Excel", "Sales", "Data Analysis", "Business Intelligence", "Reporting"]', 4, 2, 'Rapor hazırlanıyor'),
     
    (14, 'Yeni Müşteri Ziyareti', 
     'Potansiyel yeni bir kurumsal müşteriyi ziyaret edip teklif sunulacak. Ziyaret hazırlığı: 1) Müşteri hakkında araştırma yapmak (şirket profili, sektör, büyüklük, mevcut çözümler), 2) Karar vericileri belirlemek (CEO, CTO, satın alma müdürü), 3) Müşteri ihtiyaçlarını önceden anlamak (LinkedIn, web sitesi, haberler), 4) Özelleştirilmiş teklif hazırlamak (ihtiyaçlara göre özelleştirilmiş paket, fiyatlandırma), 5) Demo materyalleri hazırlamak, 6) Ziyaret günü ve saatini koordine etmek, 7) Ziyaret sırasında notlar almak ve sorular sormak, 8) Müşteri itirazlarını dinlemek ve cevaplamak, 9) Ziyaret sonrası teşekkür e-postası ve takip planı, 10) CRM sistemine ziyaret notlarını kaydetmek. Bu ziyaret, yeni bir müşteri kazanmak için önemli bir fırsattır.', 
     9, 4, CURRENT_TIMESTAMP, CURRENT_DATE + INTERVAL '4 days', 0, 2, 4, '["Müşteri İlişkileri", "Satış", "CRM", "Client Visit", "Business Development", "Account Management"]', 8, 0, NULL),
    
    -- Gecikmiş Görevler (Test için)
    (15, 'Eski Proje: Sistem Migrasyonu', 
     'Eski legacy sistemden yeni modern sisteme geçiş projesi kritik bir aşamada. Bu görev acil ve yüksek öncelikli. Migrasyon kapsamı: 1) Mevcut sistemin tam envanterini çıkarmak (tüm modüller, veritabanları, entegrasyonlar), 2) Veri analizi ve temizleme (duplicate kayıtlar, eksik veriler, format dönüşümleri), 3) Veri migrasyon scriptleri yazmak ve test etmek, 4) Yeni sistemde test ortamı kurmak, 5) Kademeli migrasyon planı hazırlamak (pilot grup, tam geçiş), 6) Kullanıcı eğitimleri düzenlemek, 7) Rollback planı hazırlamak, 8) Go-live tarihini belirlemek ve hazırlık yapmak, 9) Migrasyon sonrası destek ve monitoring. Bu proje 5 gün gecikmiş durumda ve acil müdahale gerekiyor. Sistem kesintisi olmadan geçiş yapılmalı.', 
     1, 1, CURRENT_TIMESTAMP - INTERVAL '20 days', CURRENT_DATE - INTERVAL '5 days', 1, 3, 1, '["Migration", "System Administration", "Data", "Legacy System", "Data Migration", "Project Management", "Risk Management"]', 40, 25, 'Gecikmiş, acil'),
     
    (16, 'Ertelenen: Dokümantasyon', 
     'Proje dokümantasyonu eksik kaldığı için tamamlanması gerekiyor. Dokümantasyon kapsamı: 1) Sistem mimarisi dokümantasyonu (diagramlar, teknoloji stack, altyapı), 2) API dokümantasyonu (endpoint''ler, request/response örnekleri, authentication, error codes), 3) Veritabanı şema dokümantasyonu (tablolar, ilişkiler, indexler), 4) Kullanıcı kılavuzu (adım adım kullanım talimatları, ekran görüntüleri, FAQ), 5) Geliştirici kılavuzu (kurulum, geliştirme ortamı, coding standards), 6) Deployment dokümantasyonu (production ortamı kurulumu, backup/restore prosedürleri), 7) Troubleshooting kılavuzu (yaygın sorunlar ve çözümleri), 8) Changelog ve versiyon notları. Dokümantasyon, Markdown formatında hazırlanacak ve GitHub''da tutulacak. Bu görev 3 gün gecikmiş ve ertelenmiş durumda.', 
     3, 1, CURRENT_TIMESTAMP - INTERVAL '15 days', CURRENT_DATE - INTERVAL '3 days', 0, 1, 1, '["Documentation", "Technical Writing", "API", "User Guide", "Developer Guide", "System Documentation"]', 12, 0, 'Ertelendi')
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    assigned_to_id = EXCLUDED.assigned_to_id,
    created_by_id = EXCLUDED.created_by_id,
    due_date = EXCLUDED.due_date,
    status = EXCLUDED.status,
    priority = EXCLUDED.priority,
    department_id = EXCLUDED.department_id,
    skills_required = EXCLUDED.skills_required,
    estimated_hours = EXCLUDED.estimated_hours,
    actual_hours = EXCLUDED.actual_hours,
    notes = EXCLUDED.notes;

-- ============================================
-- 5. TOPLANTILAR (Meetings)
-- ============================================
INSERT INTO meetings (id, title, description, start_time, end_time, organizer_id, location, attendee_ids, is_reminder_sent) 
VALUES 
    -- Bugünkü Toplantılar
    (1, 'Günlük Scrum Toplantısı', 'Günlük sprint durumu ve blokajlar', 
     CURRENT_DATE + INTERVAL '9 hours', 
     CURRENT_DATE + INTERVAL '9 hours 30 minutes', 
     1, 'Toplantı Odası A', '[1,2,3]', false),
    
    (2, 'Proje Planlama Toplantısı', 'Yeni özellikler için planlama', 
     CURRENT_DATE + INTERVAL '14 hours', 
     CURRENT_DATE + INTERVAL '15 hours 30 minutes', 
     1, 'Toplantı Odası B', '[1,2,4,8]', false),
    
    (3, 'Departman Toplantısı - HR', 'Aylık departman toplantısı', 
     CURRENT_DATE + INTERVAL '10 hours', 
     CURRENT_DATE + INTERVAL '11 hours', 
     6, 'Toplantı Odası C', '[5,6]', false),
    
    -- Yarınki Toplantılar
    (4, 'Müşteri Sunumu', 'Yeni müşteriye ürün sunumu', 
     CURRENT_DATE + INTERVAL '1 day' + INTERVAL '13 hours', 
     CURRENT_DATE + INTERVAL '1 day' + INTERVAL '14 hours 30 minutes', 
     9, 'Müşteri Ofisi', '[9,10]', false),
    
    -- Gelecek Hafta
    (5, 'Strateji Toplantısı', 'Q2 strateji planlaması', 
     CURRENT_DATE + INTERVAL '7 days' + INTERVAL '10 hours', 
     CURRENT_DATE + INTERVAL '7 days' + INTERVAL '12 hours', 
     4, 'Yönetim Toplantı Salonu', '[1,4,6,8,10]', false)
ON CONFLICT (id) DO UPDATE SET
    title = EXCLUDED.title,
    description = EXCLUDED.description,
    start_time = EXCLUDED.start_time,
    end_time = EXCLUDED.end_time,
    organizer_id = EXCLUDED.organizer_id,
    location = EXCLUDED.location,
    attendee_ids = EXCLUDED.attendee_ids,
    is_reminder_sent = EXCLUDED.is_reminder_sent;

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

