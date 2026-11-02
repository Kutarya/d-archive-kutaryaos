# Kutarya OS — proje devir kaydı

5 Ekim 2026. Kullanıcı yazılım dersi için yalnız C# Windows Forms OOP projesi
istedi. Mevcut KutaryaWeb/Firebase/AI servislerinden ayrı D:\KutaryaOS kuruldu.
Önce PROJE-PLANI.md, README.md ve docs/KABUL.md okunur.

## Düzen

- ProjeDosyalari/KutaryaOS.sln: Core sınıf kütüphanesi + WinForms WinExe.
- Bagimliliklar/dotnet: resmi dotnet-install.ps1 ile .NET SDK 10.0.401,
  WindowsDesktop 10.0.12. NuGet ve CLI dizinleri aynı D: kökündedir.
- Dagitim/KutaryaOS/KutaryaOS.exe: Windows x64 self-contained paket.
- Veri: kalıcı cihaz JSON/tema/DPAPI; cihaz-id/Disk ve CopKutusu.
- Çalıştırma: D:\KutaryaOS\KutaryaOS-Baslat.ps1. Bağımsız başka klasöre taşınan
  EXE veriyi kendi SanalCihazlar klasöründe tutar. --data açık override.
- Derle.ps1 build + 34 çekirdek kontrolü + 9 UI kontrolü + publish yapar.
  Test raporları ve kendi render çıktıları TestSonuclari altındadır.

## Uygulama

İlk açılış adı/RAM/GPU-VRAM/depolama; donanım GlobalMemoryStatusEx ve DXGI'den.
RAM/VRAM fiziksel tahsis değildir. Depolama kotası gerçektir.
Masaüstü/menü/görev çubuğu; dosyalar, çöp, notlar, hesap, izleyici ve ayarlar.
Üç tema, GDI+ ile özgün bitmap duvar kâğıdı. AI paneli sağdan animasyonla açılır.
Tüm UI C# WinForms; WPF/HTML/JS/Electron kullanılmaz.

AI provider IAiProvider/HttpAiProvider, yerel OpenAI-compatible chat ve bulut
Responses; model seçimi/endpoint/key DPAPI. Anahtar isteme yeri uygulama ayarıdır.
Kullanıcıya görünen yardımcının adı Kutarya Us. Model/kimlik bilgisi gelmedi;
gerçek AI kabulü açık. Kontrollü HTTP protokol testi gerçek model sayılmaz.
Eski Kutarya model promptu, servisleri, tünelleri ve hesabı değiştirilmedi.

## Gerçek dosya taşıma tuzağı

Kaynağı kopyadan önce veya doğrulamadan sonra isimle tekrar silme: isim yarışı
olabilir. ImportMoveAsync DELETE yetkili CreateFile tanıtıcısıyla kaynağı
kilitler, geçici hedef yazar, Flush(true), iki SHA-256 karşılaştırması yapar,
kalıcı hedefe geçirir; sonra AYNI tanıtıcıya FileDispositionInfo verir.
FILE_DISPOSITION_INFO.DeleteFile BOOLEAN'dır (1 byte), BOOL değildir.
Silme işaretlenemezse kaynak + doğrulanmış kopya korunur ve sonuç bunu söyler.
Salt okunur/kilit/iptal/kota kontrolü test edildi. Klasör/link aktarımı yok.

## UI render tuzağı ve sınırlar

TableLayoutPanel görev çubuğuna RowStyle Percent 100 koymadan dock düğmeleri
kesiliyordu. İlk simge paneli yüksekliği Shown/Layout ile eşitlenir.
Anchor'lı AI kapatma düğmesinin yeri header.Resize ile atanır.
İlk kurulum kısa ekranlarda kaydırılır ve oluştur düğmesi sabit alt alandadır.
DrawToBitmap çocuk Form z sırasını güvenilir fiziksel ekran kanıtı sayma;
ayrı pencere renderları var. Fiziksel fareyle sürükleme ve başka PC/DPI açık.

WinForms ilk cihazda gerçek OS boot etmez. EXE/kod dosyaları saklanabilir;
keyfi program çalıştırılmaz. AI mevcut dosyayı ezemez; editör kendi açık
belgesini kaydeder. Yeni belge çakışmasında Farklı kaydet kullanılabilir.

Web sitesi yayımlanmadı; eski AI/mobile/PayTR ve üretim Auth değiştirilmedi.
Gelecek asistan gerçek AI bağlantısını kullanıcının tercihinden tamamlayabilir.
Kaynak arşivine Veri, SanalCihazlar, DPAPI, TestSonuclari, bin/obj ekleme.

## Dağıtım EXE kabulü

Bağımsız EXE --self-test ve --ui-smoke, DOTNET_ROOT olmadan çalıştırıldı;
34/34 çekirdek ve 9/9 UI geçti. SDK ile çalışan kaynak kontrollerinin yanı sıra
bu gerçek dağıtım dosyası kontrolüdür. EXE SHA-256 paket-bilgisi.json içindedir.
Kaynak ZIP 27 girdi ve 0 kişisel veri/test/bin/obj/DPAPI girdisi içerir.
## 5 Ekim 2026 — 0.2: bağımsız yerel AI ve setup (güncel)

Önce bu bölüm geçerlidir; yukarıdaki 0.1 bağlantı/dağıtım notları tarihseldir.
Kullanıcı sunucu bağımlılığı istemiyor; kendi API veya indirilebilir yerel
model seçimi istedi. Veritabanı düşünme/kodlama/paket ekleme açıkça yasaklandı.

- Yeni paket D:\KutaryaOS\Dagitim\KutaryaOS-0.2.0\KutaryaOS.exe.
  EXE 121624428 bayt; native DLL runtimes klasörü yanındadır, tek başına taşıma.
  D:\KutaryaOS\KutaryaOS-Baslat.ps1 bu pakete güncellendi.
- D:\KutaryaOS\Dagitim\KutaryaOS-Setup-0.2.0-x64.exe gerçek Inno kurulumudur.
  Per-user, yönetici gerektirmez, kısayol ve Windows uninstall kaydı oluşturur.
  SDK gerekmez. Inno 6.7.3 D:\KutaryaOS\Bagimliliklar\InnoSetup altındadır;
  resmi kurucu hash + geçerli Authenticode doğrulandı.
- Derle.ps1: build + 34 çekirdek + 8 indirme + model varsa 16 UI/native kontrolü
  + publish + setup. KurulumKabul.ps1 yalnız D:\KutaryaOS\TestKurulum02 hedefini
  kurar, kendi aynı konumdaki kaydı yeniden kurar ve kaldırır. Başka konumdaki
  mevcut gerçek kurulumun kaydını görünce durur. Kullanıcı verisini silmez.
  Paketle.ps1 son installer/EXE hashini kabul raporuyla eşleştirmeden paketlemez.
- Default veri artık başka PC’de %LOCALAPPDATA%\KutaryaOS\Veri.
  Bu geliştirme dağıtımı/başlat betiği D:\KutaryaOS\Veri kullanır; --data override
  korunur. Uninstall bu klasörlere dokunmaz; uygulama içi JSON ve gerçek dosyalar var.
- LLamaSharp + CPU 0.27.0, NuGet D: ve packages.lock.json. Yerel AI in-process
  native CPU’dur; sunucu/Ollama/8912 zorunluluğu ve hazır servis profili kaldırıldı.
- Resmi Qwen3-0.6B Q8_0, 639446688 bayt; model kullanıcıya Kutarya Us olarak
  sunulur. Revizyon/SHA ModelDownload.School ve docs/LISANSLAR.txt içinde.
  D:\KutaryaOS\Veri\YerelAI\Kutarya-Us-Hafif.gguf indirildi/doğrulandı.
  .part + Range, boyut/GGUF/SHA; yanlış final yeniden indirilir. Model setup’a girmez.
- LocalModelManager ağırlıkları lazy yükler ve saklar; panel açılışı EnsureReadyAsync.
  2048 context, batch128, en fazla4 CPU thread, GPU0. JSON grameriyle gerçek
  sınırlı sanal araç çağrıları; tek adımlı kısa görev. Yerel mesaj sınırı1200 karakter.
  Çok adımlı karmaşık kod/model kalitesi vaat edilmez. Sohbet, klasör ve metin
  oluşturma gerçek GGUF ile doğrulandı; kaynak/çıktı taklidi yok.
- Son native dağıtım koşusu: load2.754 saniye, sohbet+klasör+metin toplam11.872
  saniye, WorkingSet719MiB. Tek koşudur, evrensel benchmark değildir.
- Gate/weights yaşam döngüsü: core await’lerinde ConfigureAwait(false) gerekir;
  UI kapanınca senkron Dispose gate beklerken UI continuation deadlock yaratma.
  AI paneli kapanış/iptali token verir. Native model decode worker’da kalır.
- Kendi API Chat Completions/Responses ve DPAPI. api-profili.dpapi seçimi
  yerel modele geçince korunur; ai.dpapi aktif moddur. Gerçek ücretli API anahtarı
  yok; yalnız protokol ve kontrollü UI profil kaydı ölçüldü, gerçek API sohbeti değil.
- Büyük hoş geldin/slogan kartları kaldırıldı; doğal dağ/göl ImageGen varlığı,
  ayrıntılı C# ikonları, özel ICO ve üç görünüm. Prompt assets/DUVAR-KAGIDI.md.
- Source/UI/kurulu paket: 34/34 çekirdek, 8/8 indirici, 16/16 UI/native,
  9/9 kurulum. Rapor TestSonuclari/Kurulum; gerçek native ayrıca Veri/yerel-ai-kabul.json.
  Installer/EXE hashleri kurulum-kabul.json ve Dagitim/paket-bilgisi-0.2.0.json.
- UI testleri UIKosular/GUID içinde veri/tema/API profili tutar; gerçek kullanıcı
  ayarlarını değiştirme ve önceki klasörün varlığına dayanarak yanlış pozitif verme.
  Renderdan önce IsBusy=false ve ConnectionStatus hazır beklenir.
- Fotoğraflar çalışan WinForms Show/PerformClick/DrawToBitmap renderlarıdır;
  fiziksel ekran fotoğrafı değildir. Son galeri Dagitim/UYGULAMA-GORUNTULERI.md.
  Fiziksel Explorer drag-drop, okul PC/DPI/CPU ve sınıf provası açık kabul sınırıdır.
- 14 iş günü ders planı ve puanlı risk matrisi docs/GELISTIRME-PLANI.md ile
  docs/RISK-HARITASI.md. Süreler/puanlar tahmin, uygulama kabulü gerçek ölçümdür.
- Kaynak ZIP’e Veri/SanalCihazlar/YerelAI/model, DPAPI, test, bin/obj alınmaz.
  Eski 0.1 EXE ilk kurulumda açık olabilir; kill etmeden kullanıcı kapatır.
  Son sürüm ayrı klasörde yayımlanır, açık eski EXE üzerine publish yapma.
- KutaryaWeb/Firebase/Auth/admin/AI/mobile/PayTR/üretim ayarları değişmedi.
  Site deploy alınmadı; eski canlı servislerin test/deploy kuralları bu işe taşınmadı.


## 5 Ekim 2026 — 0.3: yalnız sanal Masaüstü ve kullanıcı dışa aktarımı

Kullanıcı önce durdurdu, ardından "Ekle o zaman yap ve bitir" diyerek bu iki
değişikliği yetkilendirdi. Önceki geniş AI sanal disk erişimi artık geçerli değil.

- VirtualToolExecutor.DesktopPath her sağlayıcı yolunu kodda sınırlar:
  yalnız Masaustu ve altı. Yalın ad Masaustu altına alınır; Belgeler/Projeler/
  Resimler, mutlak/UNC/kolon/../junction yolları reddedilir. list/read de sınırlı;
  rename hem kaynak hem hedefi denetler, Masaustu kökünü taşıyamaz.
- Sistem metni, araç açıklamaları ve yerel few-shot örneği bu sınırla eşleşir.
  Host aktarım aracı AI sözleşmesine eklenmedi. open_app yalnız mevcut izinli
  sanal uygulamaları açar; hedef seçemez/aktarımı başlatamaz.
- Dosyalar panelinde ayrı alt satır: öğe seç → Ana PC'ye taşı → hedef klasör
  seç → Taşımayı başlat. HostExportForm kullanıcı seçimiyle native
  FolderBrowserDialog açar; iptal ve kaydırılabilir sonuç alanı var.
- VirtualFileSystem.ExportMoveAsync dosya, iç içe ve boş klasör taşır.
  Kaynak dosyaları read+DELETE tanıtıcılarıyla yazma/silme yarışına karşı kilitler;
  klasör tanıtıcıları yeniden adlandırmayı engeller. Geçici hedef + Flush(true)
  + her dosya SHA-256; bütün hedef hazır olmadan hiçbir kaynak kaldırılmaz.
  Kaynak ağacı kopyalama sırasında değişirse iptal edilir. Çakışmada yeni ad
  kullanılır. Silme aşamasında sorun varsa doğrulanmış host kopyası ve kalan
  sanal öğeler korunur; Moved=false raporlanır. Diskler arası atomiklik iddiası yok.
- Sanal ana klasörler, sanal cihaz içinde hedef, UNC/ağ/junction, salt okunur/
  kilitli dosya reddedilir. Limit: 5000 dosya, 256 klasör, 64 derinlik.
  Ağır iş UI worker'ında; core await ConfigureAwait(false). Kullanıcı iptali
  commit öncesi kaynakları korur; commit sonrası doğrulanmış hedef geri silinmez.
- UI tuzağı: FlowLayoutPanel'i fill ListView'dan önce eklemek düğmeyi örter.
  Visible=true testi tek başına yeterli değil. Dock sırası düzeltildi, listeyle
  kesişmeme kontrolü ve gerçek render incelemesi var; uzun düğmeler genişletildi.
  Multiline TextBox sonuç satırları Environment.NewLine ister.
  Progress<T> callback'i sonuçtan sonra kuyruktan gelebilir; IsTransferring=false
  ise yok say. UI testi DoEvents ile kuyruğu boşaltıp terminal başarı metnini de
  denetler; dosyanın taşınmış olması tek başına UI sonucu kanıtı değildir.
- Yerel AI testi eski "Model metin döndürmedi." fallback'ini başarı sayıyordu.
  Bu yanlış pozitif düzeltildi. Native sohbet gramerinde none için boş text
  yasak; boş/whitespace sonuç IOException. Kabul tam dosya içeriğini karşılaştırır.
  Küçük modelin bilgi/çok adımlı görev kalitesi yine garanti edilmez.
- Kaynak, publish ve son setup 0.3.0. 34/34 core + 34/34 sınır/aktarım +
  8/8 indirme + 20/20 UI/native; kurulum 10/10. UI hedef seçimi test fixture
  klasörüyle enjekte edilir, gerçek düğme gerçek dosyaları taşır. Fiziksel
  FolderBrowserDialog/Explorer fare akışı, başka PC/DPI ve sınıf provası açık.
- Son kurulu EXE, SDK olmadan D:\KutaryaOS\TestKurulum03 içinde test edildi.
  Setup hash 62911E0AA5AFCE674B6E64F63D4558BE029A3C0BDC11309A3CC2FC72D89D2174;
  EXE hash 6B351B18BB389AC8C78F24137A8B675A7BD13267455D649BC87B88E395FF7920.
  KurulumKabul.ps1 farklı gerçek kullanıcı kurulumunu değiştirmez.
  Yeniden kurulum ve uninstall kendi kullanıcı test dosyasını korudu.
- Bağımsız 0.3 EXE gerçek GGUF koşusu: sohbet "Merhaba!", Masaustu/Okul,
  Masaustu/merhaba.txt içeriği "Merhaba Kutarya."; yükleme1.321s,
  toplam9.006s, WorkingSet720MiB. Tek koşu; Veri/yerel-ai-kabul.json exe yolunu
  ve tarihi kaydeder. Gerçek ücretli API konuşması yapılmadı.
- Güncel setup/portable/source adları 0.3.0; başlat betiği yeni klasöre bakar.
  Raporlar TestSonuclari/Kurulum; son paket manifesti Dagitim/paket-bilgisi-0.3.0.json.
  Galeri Dagitim/UYGULAMA-GORUNTULERI.md. Kaynak ZIP filtresi özel veri/model/
  DPAPI/test/bin/obj dışlar; Paketle.ps1 son setup/EXE kabul hashini zorunlu tutar.
  Eski 0.1 ve 0.2 dağıtımları korundu; açık 0.1 kullanıcı penceresi kapatılmadı.
- DB eklenmedi. KutaryaWeb/Firebase/Auth/admin/AI/mobile/PayTR ve canlı servisler
  değiştirilmedi; deploy yok. README, plan, OOP, risk ve kabul belgeleri güncellendi.


