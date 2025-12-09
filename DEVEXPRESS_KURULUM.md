# 🔧 DevExpress Toolbox ve Referans Ekleme Rehberi

## Durum
DevExpress kurulu ve "DevExpress Assembly Deployment Tool" görünüyor, ancak kontroller Toolbox'ta yok.

## Adım 1: DevExpress DLL Konumunu Bulma

1. Windows'ta `C:\Program Files\DevExpress` klasörünü açın
2. En son sürüm klasörünü bulun (örn: `23.2` veya `24.1`)
3. Şu yolu not edin: `C:\Program Files\DevExpress XX.X\Components\Bin\Framework\`

**Alternatif Yol:**
- Visual Studio'da `Tools > DevExpress > Assembly Deployment Tool` açın
- Bu araç DevExpress DLL'lerinin konumunu gösterir

## Adım 2: DevExpress Referanslarını Projeye Ekleme

### Yöntem 1: Visual Studio'dan Ekleme

1. **Solution Explorer**'da projeye sağ tıklayın
2. `Add > Reference...` seçin
3. `Browse` butonuna tıklayın
4. DevExpress DLL klasörüne gidin (yukarıdaki yol)
5. Şu DLL'leri seçin (Ctrl tuşu ile çoklu seçim):
   - `DevExpress.Data.vXX.X.dll` (XX.X = sürüm numarası)
   - `DevExpress.Utils.vXX.X.dll`
   - `DevExpress.XtraEditors.vXX.X.dll`
   - `DevExpress.XtraGrid.vXX.X.dll`
   - `DevExpress.XtraCharts.vXX.X.dll`
   - `DevExpress.XtraBars.vXX.X.dll`
   - `DevExpress.XtraLayout.vXX.X.dll`
   - `DevExpress.XtraNavBar.vXX.X.dll`
   - `DevExpress.XtraScheduler.vXX.X.dll` (opsiyonel)
6. `OK` butonuna tıklayın

### Yöntem 2: Manuel .csproj Düzenleme

Eğer DLL konumunu biliyorsanız, `.csproj` dosyasına manuel olarak ekleyebilirsiniz.

## Adım 3: Toolbox'a DevExpress Kontrollerini Ekleme

### Yöntem 1: Otomatik Ekleme (Önerilen)

1. Visual Studio'yu **yönetici olarak** çalıştırın
2. `Tools > DevExpress > Toolbox Designer` menüsünü açın
3. `Add Controls` butonuna tıklayın
4. İhtiyacınız olan kontrolleri seçin:
   - ✅ GridControl
   - ✅ ChartControl
   - ✅ RibbonControl
   - ✅ TileView
   - ✅ LayoutControl
   - ✅ NavBarControl
5. `OK` butonuna tıklayın
6. Visual Studio'yu yeniden başlatın

### Yöntem 2: Manuel Toolbox Ekleme

1. Visual Studio'da `View > Toolbox` menüsünü açın (veya `Ctrl+Alt+X`)
2. Toolbox'ta boş bir alana sağ tıklayın
3. `Choose Items...` seçin
4. `.NET Framework Components` sekmesine gidin
5. `Browse...` butonuna tıklayın
6. DevExpress DLL klasörüne gidin
7. Şu DLL'leri seçin:
   - `DevExpress.XtraGrid.vXX.X.dll` → GridControl ekler
   - `DevExpress.XtraCharts.vXX.X.dll` → ChartControl ekler
   - `DevExpress.XtraBars.vXX.X.dll` → RibbonControl ekler
   - `DevExpress.XtraEditors.vXX.X.dll` → Diğer editörler
8. `OK` butonuna tıklayın
9. Toolbox'ta "DevExpress" sekmesi oluşacak

### Yöntem 3: DevExpress Toolbox Reset

1. Visual Studio'yu kapatın
2. `Tools > DevExpress > Toolbox Designer` açın
3. `Reset Toolbox` butonuna tıklayın
4. Visual Studio'yu açın
5. Toolbox'ı kontrol edin

## Adım 4: DevExpress Kontrollerini Kullanma

Referanslar eklendikten sonra kodda kullanabilirsiniz:

```csharp
using DevExpress.XtraGrid;
using DevExpress.XtraCharts;
using DevExpress.XtraBars.Ribbon;
```

## Adım 5: Projeyi Test Etme

1. Projeyi derleyin (`Build > Build Solution`)
2. Hata varsa, eksik referansları kontrol edin
3. Form Designer'da DevExpress kontrollerini görebilmelisiniz

## ⚠️ Sık Karşılaşılan Sorunlar

### "DevExpress DLL bulunamadı" hatası
- DLL konumunu doğru girdiğinizden emin olun
- Sürüm numarasını kontrol edin (v23.2, v24.1, vb.)
- DLL'lerin mevcut olduğundan emin olun

### "Toolbox'ta DevExpress kontrolleri görünmüyor"
- Visual Studio'yu yönetici olarak çalıştırın
- `Tools > DevExpress > Register Controls` çalıştırın
- Visual Studio'yu yeniden başlatın

### "Lisans hatası"
- DevExpress trial sürümü kullanıyorsanız, lisans ekranı çıkabilir
- Trial sürümü için kayıt olmanız gerekebilir

## 📝 Notlar

- DevExpress sürüm numarası (v23.2, v24.1, vb.) önemlidir
- Farklı sürümler birbiriyle uyumlu olmayabilir
- Projede kullanılan tüm DevExpress DLL'lerinin aynı sürümde olması gerekir

## 🎯 Hızlı Kontrol Listesi

- [ ] DevExpress DLL konumu bulundu
- [ ] Projeye DevExpress referansları eklendi
- [ ] Toolbox'a DevExpress kontrolleri eklendi
- [ ] Visual Studio yeniden başlatıldı
- [ ] Proje hatasız derlendi
- [ ] Form Designer'da DevExpress kontrolleri görünüyor

