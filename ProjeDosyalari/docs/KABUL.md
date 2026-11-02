# Kutarya OS 0.3 — kabul ve teslim

5 Ekim 2026. Uygulama yalnız C# / Windows Forms. Kullanılan normal ürün akışında
mock AI veya zorunlu Kutarya sunucusu yoktur. Yerel model native CPU ile aynı
uygulama sürecinde çalışır; kendi API seçeneği kullanıcıya aittir.

| Kontrol | Sonuç | Kapsam |
|---|---:|---|
| Release / publish | 0 hata, 0 uyarı | SDK ve bağımsız Windows x64 paket |
| Çekirdek | 34/34 | Donanım, bütçe, kota, dosya taşıma, kaynak koruması, çöp, DPAPI, iki HTTP protokolü |
| AI sınırı / dışa aktarım | 34/34 | Yalnız Masaüstü; dış kaynak/hedef reddi; iç içe/boş klasör, çakışma, iptal, kilit, junction, gerçek D→C aktarımı |
| Model indirme | 8/8 | Boyut/SHA, bozuk final dosya, Range, iptal/devam, yerel mod ayarı |
| WinForms + gerçek yerel AI | 20/20 | Gerçek pencereler, AI klasör aracı, API profili, hedef seçiminden sonra gerçek taşıma düğmesi, kaynak kaldırma |
| Setup | 10/10 | Kurulan EXE hash, Windows kaydı, dört kurulu uygulama test takımı, yeniden kurulum, veri koruyan kaldırma |
| Gerçek native AI | Sohbet + klasör + dosya | Resmi GGUF, sahte sağlayıcı olmadan gerçek çıkarım |

**Son setup** D:\KutaryaOS\TestKurulum03 içine kurularak çalıştırılır; program
kaldırılırken yalnız test için oluşturulan kullanıcı dosyası korunur.
KurulumKabul.ps1 bu kabulü tekrarlar; farklı gerçek kurulum varsa üzerine yazmaz.

## Kanıt dosyaları

Geliştirme bilgisayarındaki TestSonuclari/Kurulum altında:

- Core/kabul.json — kurulu bağımsız EXE çekirdek sonuçları
- Aktarim/aktarim-kabul.json — AI Masaüstü sınırı ve gerçek dosya sistemi aktarımı
- Model/model-kabul.json — kontrollü indirici testleri
- UI/ui-kabul.json — kurulu EXE ve gerçek native AI paneli kontrolleri
- kurulum-kabul.json — setup/yeniden kurulum/kaldırma ve paket SHA-256
- UI/*.png — çalışan WinForms kontrol ağacının görüntüleri

Veri/yerel-ai-kabul.json gerçek modelin sohbet, klasör ve metin dosyası
sonuçlarını tutar. Yeni sürümde öğeler yalnız Masaustu/Okul ve
Masaustu/merhaba.txt içinde oluşturulur. Tam dosya içeriği karşılaştırılır;
"Model metin döndürmedi" mesajı başarılı sohbet sayılmaz. Yerel sohbet JSON
grameri boş text alanını kabul etmez; bu, yanıtın bilgi doğruluğu garantisi değildir.
Gecikme ve bellek ölçümleri raporda yer alır; başka PC'ye performans sözü değildir.
Model 639.446.688 bayt; sabit revizyon ve SHA-256 LISANSLAR.txt içindedir.

İndirme ve HTTP protokol testleri kontrollü sunucu kullanır; bu test
sunucuları gerçek AI olarak sayılmaz. Gerçek model kabulü ayrı GGUF çıkarımıdır.
API ayarı UI testinde kontrollü geçersiz alan adıyla kaydedilir, dışarı istek
gönderilmez. Kullanıcının ücretli API hesabıyla gerçek sohbet ölçülmedi.
Aktarım UI testinde klasör seçimi kontrollü geçici hedefle verilir; gerçek
düğmenin başlattığı dosya taşıma, içerik ve kaynak kaldırma doğrulanır.
Ürün akışı Windows FolderBrowserDialog kullanır; AI hedef seçemez.

## Görüntülerin kapsamı

Görüntüler çalışan gerçek Form’ların Show/PerformClick/DrawToBitmap akışından
alındı. Bir tasarım maketi veya AI tarafından üretilmiş uygulama görüntüsü değildir.
Fiziksel ekran yakalama da değildir. Duvar kâğıdı ayrı bir ImageGen varlığıdır.
Çocuk Form’ların DrawToBitmap z sırası fiziksel ekran sırası kanıtı sayılmaz;
uygulama pencereleri ayrıca çekildi.

## Açık kabul sınırları

- Okul bilgisayarında fiziksel Explorer drag-drop ve Windows klasör seçicisi
  fareyle denenmedi. Gerçek dosya taşıma/silme, kilit/iptal/kota ve hash,
  UI düğmesi ve farklı sürücüler bu bilgisayarda test edildi.
- Başka PC’de 125/150% DPI, ekran boyutu ve native CPU uyumu ölçülmedi.
  960×640 kendi WinForms kontrol ağacında kontrol edildi.
- Hafif modelin karmaşık kod veya uzun çok adımlı görevlerde doğruluk garantisi yok.
  Sonuçlar gerçek dosyadan doğrulanır; mevcut dosya AI tarafından ezilmez.
- Yeni sürüm numarasına geçiş ayrıca test edilmedi; 0.3 setup'ın yeniden kurulumu
  ve kullanıcı dosyasını koruması test edildi.
- Setup ticari kod imzası taşımıyor; Windows uyarısı oluşabilir.

Geliştirme planının günleri tahmindir. Risk puanları tasarım tahminidir.
Veritabanı kodu/paketi eklenmedi. Web/Hosting/Auth/PayTR/AI sunucuları değiştirilmedi.

## Taşımanın güvenlik sınırı

Kaynak kilitlenir, dosyalar geçici hedefe kopyalanır ve tek tek SHA-256 ile
doğrulanır. Bütün hedef hazır olmadan hiçbir sanal kaynak kaldırılmaz. Hedefte
aynı isim korunur. Sonraki silme aşamasında hata olursa doğrulanmış ana PC
kopyası ve kalan sanal öğeler korunur; tam taşındı diye raporlanmaz.
Diskler arası atomik işlem, ani elektrik kesilmesi veya işletim sistemi
düzeyinde sandbox garantisi verilmez. Sanal ana klasörler, ağ yolları,
bağlantılar ve salt okunur/kilitli öğeler reddedilir.

