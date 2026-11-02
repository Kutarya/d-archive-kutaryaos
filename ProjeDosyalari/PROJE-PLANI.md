# Kutarya OS — 0.3 kapsamı

Yalnız C# Windows Forms; gerçek OS/hypervisor değil, dosyaları gerçek olan
masaüstü simülasyonu. İlk açılış donanım sınırları, cihaz adı, kaynak bütçeleri;
masaüstü, uygulama menüsü, görev çubuğu ve sağ AI paneli.

| Bölüm | Davranış |
|---|---|
| Cihaz | Gerçek RAM, DXGI GPU/VRAM, disk boşluğu; JSON/tema kalıcılığı |
| Masaüstü | Sade çalışma alanı, doğal dağ/göl görseli, üç görünüm, özel ikon |
| Uygulamalar | Dosyalar, çöp kutusu, notlar, hesap, kaynak izleyici, ayarlar |
| Dosyalar | Kota, güvenli içe taşıma, kullanıcının seçtiği ana PC klasörüne dosya/klasör taşıma, ad çakışması, çöp ve geri yükleme |
| Yerel AI | Resmi doğrulanmış GGUF indirme; native CPU; panel açılınca yükleme |
| Kendi API | Kullanıcının adres/model/anahtarı; DPAPI; iki protokol |
| AI araçları | Yalnız sanal Masaüstünde liste/okuma/klasör/yeni metin ve izinli uygulamalar; ana PC aktarımı yetkisi yok |
| Teslim | Self-contained EXE, gerçek setup, kaynak ZIP, fotoğraflar ve ders belgeleri |

Geliştirme takvimi docs/GELISTIRME-PLANI.md içindedir. Süreler öğrencinin
yeniden geliştirmesi için tahmindir; mevcut sürümün gerçek çalışma süresi değildir.
Risk haritası docs/RISK-HARITASI.md, kanıt docs/KABUL.md.

Kullanıcının son yönlendirmesi: ortak/özel Kutarya sunucusuna bağımlılık
kurma; kendi API veya yerel model seçimi. Veritabanı bu işin kapsamına alınmadı.
Kurulumun kaldırılması cihaz verisini ve indirilmiş modeli kaldırmaz.
Okul PC'sinde elle drag-drop, klasör seçicisi, DPI ve sunum provası kullanıcı kabulüdür.

