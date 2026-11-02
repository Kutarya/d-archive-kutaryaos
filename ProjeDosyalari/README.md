# Kutarya OS 0.3

Yazılım dersi için C# / Windows Forms masaüstü simülasyonu. Arayüzde başka bir
UI çatısı kullanılmaz. Kullanıcı veritabanı çalışmasını kendisi yürüteceği için
bu sürüme veritabanı kodu veya paketi eklenmedi.

## Kurulum ve kullanım

Dagitim klasöründeki **KutaryaOS-Setup-0.3.0-x64.exe** kurulabilir pakettir.
Windows x64 için .NET çalışma zamanı dahildir; okul PC’sine SDK gerekmez.
Kurulum kullanıcı hesabında yapılır, yönetici yetkisi gerektirmez. Kısayol
oluşturabilir ve Windows Uygulamalar bölümünden kaldırılabilir.

Taşınabilir alternatif: KutaryaOS-Windows-x64-0.3.0.zip dosyasını tamamen aç,
KutaryaOS.exe çalıştır. Native DLL klasörlerini EXE’den ayırma.

Bu geliştirme bilgisayarında D:\KutaryaOS\KutaryaOS-Baslat.ps1 yeni 0.3 paketini
D:\KutaryaOS\Veri ile açar. Eski 0.1 penceresi açıksa önce onu kapat.
Diğer bilgisayarlarda varsayılan veri klasörü:
%LOCALAPPDATA%\KutaryaOS\Veri. --data ile başka klasör seçilebilir.
Kurulumun kaldırılması bu kullanıcı verisini silmez.

İlk açılışta cihaz adı, RAM/VRAM bütçesi ve depolama kotasını seç.
RAM/VRAM gerçek donanım rezervasyonu değildir. Dosyaların depolama kotası gerçektir.
Sol simgeler uygulamaları, alttaki kutarya düğmesi menüyü açar. Küçültülen pencere
görev çubuğundan geri açılır. Kutarya Us veya Ctrl+Space sağ AI panelini açar.

## AI — iki seçenek

| Seçenek | Nasıl kullanılır | Bağlantı |
|---|---|---|
| Yerel model indir | Ayarlar → Kutarya Us → Yerel model indir; Modeli indir / Yerel modeli kullan | İlk indirme internet, sonrası CPU ile çevrimdışı |
| Kendi API’ni bağla | API adresi, model kimliği, anahtar ve protokolü gir; API’yi bağla | Kullanıcının seçtiği sağlayıcı |

Yerel seçenek 639.446.688 baytlık resmi Qwen3-0.6B Q8_0 dosyasını doğrulayarak
indirir. Kullanıcıya görünen yardımcı adı Kutarya Us’tur. .part dosyası ve
HTTP Range ile indirme sürer; boyut, GGUF ve sabit SHA-256 kontrol edilir.
Model setup’a gömülü değildir. Model indirilip seçildiğinde AI panelinin açılması
otomatik CPU yüklemesini tetikler. Ayrı sunucu, Ollama veya eski Kutarya
servisleri gerekmez. İlk yüklemeden sonra aynı ağırlıklar tekrar kullanılır.

Örnekleri **ayrı mesajlar** olarak dene:

- “Masaustu içinde Okul adında bir klasör oluştur.”
- “Masaustu/merhaba.txt dosyasını oluştur. Dosya içeriği: Merhaba Kutarya.”
- “Hesap makinesini aç.”

Hafif yerel model kısa sohbet ve tek adımlı dosya işlemleri içindir. Tek mesajı
1.200 karakterden kısa tut. Karmaşık kod ve çok adımlı görevlerde hata yapabilir.
JSON grameri çıktı biçimini sınırlar; cevabın doğruluğunu garanti etmez.
Dosya yazıldı iddiası ancak araç gerçekten çalışınca gösterilir.
Dosyalar uygulamasıyla sonucu kontrol edebilirsin. Kaynak kod derlenip
çalıştırılmaz. AI keyfi shell, EXE veya host dosya silme komutu çalıştıramaz.

AI yalnız **sanal Masaüstü ve alt klasörlerinde** listeleme, okuma, oluşturma
ve yeniden adlandırma yapabilir. Bu sınır hem kendi API hem yerel model için
dosya işlem kodunda uygulanır. Belgeler/Projeler/Resimler ve ana PC yolları
reddedilir. AI ana PC'ye aktarım başlatamaz veya hedef klasör seçemez.

Kendi API yöntemi Chat Completions ve Responses destekler. Anahtar DPAPI ile
bu Windows hesabına bağlı şifrelenir. Anahtar kaynak veya setup içine girmez.
Bu yöntemde mesajlar/okutulan içerik seçilen sağlayıcıya gider, kullanım onun
hesabından ücretlenebilir. Gerçek ücretli API sohbeti kimlik bilgisi olmadan
ölçülmedi; yerel model gerçek çıkarımla ayrıca doğrulandı.

## Dosyalar

Gerçek DOSYALARI masaüstüne veya Dosyalar listesine sürüklemek taşıma yapar:
kilitli kaynak tanıtıcısı, geçici kopya, SHA-256, Flush(true), kalıcı hedef,
ardından aynı tanıtıcıdan kaynak kaldırma. Hata/iptal/kota durumunda kaynak
korunur. Silme başarısızsa doğrulanmış kopya ve kaynak birlikte kalır.
Ana PC'den içeri sürüklemede klasör yerine içindeki dosyaları seç.
Bağlantı/junction aktarımı desteklenmez.

**Sanal cihazdan ana PC'ye:** Dosyalar uygulamasında dosya veya klasörü seç →
**Ana PC'ye taşı** → hedef klasör seç → **Taşımayı başlat**.
İç içe ve boş klasörler korunur. Her dosya SHA-256 ile doğrulanır; hedef hazır
olduktan sonra sanal kaynak kaldırılır. Aynı isimde hedef varsa `(2)` eki
kullanılır, mevcut öğeler ezilmez. Hata veya kopyalama sırasında iptal kaynakları
korur. Hedef tamamlandıktan sonra kaynak tamamen kaldırılamazsa kalan sanal
öğeler ve doğrulanmış ana PC kopyası birlikte korunur, durum açıkça bildirilir.

Sanal ana klasörlerin kendisi taşınmaz; içindeki öğeyi seç. Kilitli/salt okunur
dosyalar, ağ yolları ve bağlantılar reddedilir. Tek aktarım en fazla 5.000 dosya,
256 klasör ve 64 klasör derinliği içindir; büyük projeyi alt klasörlere ayır.

Silinen sanal dosya çöp kutusuna taşınır; geri yükleme mevcut dosyayı ezmez.
AI yeni dosyayı mevcut dosya üstüne yazamaz. Açık notlar düzenleyicide kaydedilir.

## Derleme ve belgeler

Bağımlılıklar D:\KutaryaOS\Bagimliliklar altında: .NET SDK 10.0.401, NuGet,
LLamaSharp 0.37.0 CPU ve Inno Setup 6.7.3. Kaynaktan ilk restore internet ister.
Derle.ps1 build, çekirdek, AI sınırı/aktarım, indirme ve UI kontrollerini çalıştırır;
paketler. KurulumKabul.ps1 setup'ı ayrı test klasörüne kurar ve kabul eder;
Paketle.ps1 kabul edilen aynı setup/EXE'den kaynak ve taşınabilir ZIP üretir.
KutaryaOS.sln Visual Studio WinForms/C# geliştirme araçlarıyla açılabilir.

- docs/OOP-ANLATIMI.md — sınıf haritası ve ders gösterimi
- docs/GELISTIRME-PLANI.md — tahmini 14 iş günü, adım/süre/çıktı tabloları
- docs/RISK-HARITASI.md — puanlı risk tablosu ve olasılık/etki matrisi
- docs/KABUL.md — gerçek doğrulama ve açık sınırlar
- docs/LISANSLAR.txt — üçüncü taraf lisansları
- assets/DUVAR-KAGIDI.md — ImageGen varlığı ve kullanılan istem

Kaynak ZIP’e gerçek Veri, model, API anahtarı, DPAPI, test verisi ve bin/obj
alınmaz. Web sitesi ve canlı Kutarya servisleri bu görevde değiştirilmedi.

