# Kutarya OS — ders için geliştirme planı

Süreler **tek öğrencinin ders çalışmasına göre tahmini iş günüdür**; bu oturumda
gerçekte geçen süreyi ifade etmez. Önerilen toplam 14 iş günü, yaklaşık üç ders
haftasıdır. 0.3 sürümündeki uygulama ve teslim dosyaları hazırlanmıştır; okul
bilgisayarında kullanıcı kabulü teslimden önce yapılmalıdır.

| Adım | İş | Süre | Önerilen gün | Çıktı / kabul | 0.3 durumu |
|---|---|---:|---|---|---|
| 1 | İhtiyaçlar ve sınırlar | 1 gün | 1 | Cihaz simülasyonu, C# WinForms, dosya ve AI kapsamı | Hazır |
| 2 | OOP sınıfları ve arayüzler | 1 gün | 2 | Core/UI ayrımı, IAiProvider, VirtualApplication | Hazır |
| 3 | Donanım ve ilk açılış | 1 gün | 3 | Gerçek RAM/GPU/disk sınırları; kalıcı cihaz | Hazır |
| 4 | Masaüstü, menü, görev çubuğu | 2 gün | 4–5 | Pencere aç/kapat/küçült, kaynak bütçesi | Hazır |
| 5 | Dosyalar, taşıma ve çöp | 2 gün | 6–7 | Kota, SHA doğrulama, kaynak koruması, klasörü ana PC'ye taşıma, geri yükleme | Hazır |
| 6 | Notlar, hesap ve izleyici | 1 gün | 8 | Gerçek C# sanal uygulamalar | Hazır |
| 7 | Yerel AI ve kendi API seçimi | 2 gün | 9–10 | Doğrulanmış model indirme, otomatik yükleme, yalnız Masaüstünde dosya araçları | Hazır |
| 8 | Görünüm ve kısa arayüz metinleri | 1 gün | 11 | Üç tema, özgün duvar kâğıdı, sade ayarlar | Hazır |
| 9 | Hata, kota ve hedef cihaz kontrolleri | 2 gün | 12–13 | Otomatik testler + okul PC’sinde elle kullanım | Otomatik kontroller hazır; okul PC’si açık |
| 10 | Setup, belgeler ve ders provası | 1 gün | 14 | Setup EXE, kaynak ZIP, OOP ve risk anlatımı | Paket hazır; sınıf provası kullanıcıyla |

## Ders gösterimi — 8–10 dakika

| Dakika | Gösterilecek işlem | Anlatılacak OOP konusu |
|---:|---|---|
| 0–1 | Cihaz adını ve sınırlarını belirle | MachineConfig: kapsülleme ve doğrulama |
| 1–2 | Menüden iki farklı uygulama aç | VirtualApplication: kalıtım ve çok biçimlilik |
| 2–4 | Deneme dosyası oluştur, yeniden adlandır, çöpe at ve geri yükle | VirtualFileSystem, olaylar ve sorumluluk ayrımı |
| 4–5 | Notu kaydet; kaynak izleyiciyi aç | Dosya olayı, IDisposable kaynak yaşam döngüsü |
| 5–7 | Yerel AI panelini aç; klasör ve metin dosyası iste | IAiProvider, RoutingAiProvider, sanal araç sınırı |
| 7–8 | Temayı değiştir ve uygulamayı tekrar aç | Ayarların kalıcılığı |
| 8–10 | Sınıf haritası, risk tablosu ve test raporunu göster | Test edilebilirlik ve dürüst kapsam |

Modeli okul sunumundan önce indirip bir kez dene. Yerel model kısa Türkçe sohbet
ve basit tek adımlı dosya işlemleri için seçilmiştir; karmaşık kod üretimi vaat
edilmez. Kendi API yöntemi tamamen isteğe bağlıdır.
