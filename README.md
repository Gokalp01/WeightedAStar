# 🚗 Road Graph Shortest Paths

Bu proje, **C# Console Application** olarak; **.xodr (OpenDRIVE)** ve **.osm (OpenStreetMap)** dosyalarını parse edip **graph**’a dönüştürür ve üzerinde **Dijkstra**, **A*** ve **Weighted A*** algoritmalarıyla en kısa yol çözümleri üretir.

---

## ✨ Özellikler

- **Harita Desteği**
  - `.xodr` ve `.osm` dosyalarını okuyup yönlü/ağırlıklı **graph** yapısına çevirir.
- **Algoritmalar**
  - **Dijkstra** – kesin, heuristicsiz en kısa yol.
  - **A*** – admissible heuristic ile hızlandırılmış arama.
  - **Weighted A*** – `f = g + w·h` (w>1) ile daha agresif arama.
- **Çıktılar**
  - Toplam maliyet (mesafe/süre), gezilen düğüm sayısı, bulunan yol (node id dizisi), süre (ms).

---

## 📦 Kurulum

> Proje .NET 6+ ile uyumludur (Windows/Linux/macOS).

```bash
git clone https://github.com/Optimal-Route-Academy/WeightedAStar.git
cd WeightedAStar
dotnet build
```

---

## ⚙️ Komut Satırı Parametreleri

| Parametre        | Zorunlu | Açıklama |
|------------------|:-------:|---------|
| `--map`          |   ✔    | Harita dosyası yolu (`.xodr` / `.osm`) |
| `--format`       |   ✔    | `xodr` veya `osm` |
| `--algorithm`    |   ✔    | `dijkstra`, `astar`, `weighted-astar` |
| `--source`       |   ✔    | Başlangıç düğüm ID |
| `--target`       |   ✔    | Hedef düğüm ID |
| `--weight`       |   ✖    | Weighted A* için ağırlık (örn. `1.2`, `1.5`, `2.0`) |
| `--heuristic`    |   ✖    | `euclidean` veya `manhattan`  |

---

## 📂 Proje Yapısı

```
📁 WeightedAStar
 ├─ AStarAlgorithm.cs
 ├─ DijkstraAlgorithm.cs
 ├─ GraphData.cs
 ├─ MapParser.cs
 ├─ Program.cs
 ├─ ConsoleApp3.csproj
 └─ LICENSE.txt
```

---

## ✅ Doğrulama & Test

- **Doğruluk:**
  - Küçük yapay graph’larda (3-10 düğüm) el ile hesaplanmış sonuçlarla karşılaştırın.
  - A* ve Dijkstra’nın aynı maliyeti vermesini bekleyin (`w=1` ve admissible `h` için).
- **Performans:**
  - Büyük `.osm` kesitleri için benchmark yapın.
  - Heuristik ve `w` parametresi etkisini tabloya dökün.

---

## ⚠️ Bilinen Sınırlamalar

- `.xodr`’da kompleks şerit birleşmeleri/ayrılmaları tam destekli olmayabilir.
- `.osm`’da **turn restrictions** ve `oneway` kuralları kısmi olabilir.
- Coğrafi koordinat → düzlem dönüşümü basitleştirilmiş olabilir.

---

## 🧩 Geliştirme Yol Haritası

- [ ] OSM **turn restriction** ve `oneway` tam desteği
- [ ] Hız limiti / yol sınıfı tabanlı **süre** maliyeti
- [ ] **Haversine**/projeksiyon opsiyonları
- [ ] Dinamik edge weight (yol kapama, trafik)
- [ ] Basit bir **SVG/PNG** görselleştirici

---

## 🤝 Katkı

Pull request ve issue’larınızı bekliyoruz. Kod stilinde C# konvansiyonlarına uyun.

---

## 📄 Lisans

Bu proje **MIT** lisansı ile lisanslanmıştır. Ayrıntılar için `LICENSE.txt` dosyasına bakın.
