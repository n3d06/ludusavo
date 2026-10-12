# sav (Game Save CLI)

> Công cụ dòng lệnh (CLI - .NET 8) cực kỳ nhanh gọn giúp tự động quét, sao lưu, nén và đồng bộ file save game lên GitHub repository riêng tư, sử dụng dữ liệu từ [Ludusavi](https://github.com/mtkennerly/ludusavi).

---

## ✨ Tính năng nổi bật

- **Tên lệnh siêu ngắn**: Đúng 3 ký tự `sav`, đọc 1 âm tiết dứt khoát như `git`.
- **Giao diện terminal trực quan**: Sử dụng `Spectre.Console` hiển thị bảng màu, trạng thái loading/spinner và menu tương tác.
- **Hỗ trợ 50.000+ tựa game**: Tự động nhận diện thư mục save trên Windows (`%APPDATA%`, `%LOCALAPPDATA%`, `Saved Games`, Documents,...).
- **Sao lưu an toàn & toàn vẹn**: Nén ZIP kèm mapping đường dẫn portable và mã kiểm tra SHA-256.
- **Lưu trữ đám mây riêng tư**: Đẩy và kéo save game trực tiếp từ GitHub repository cá nhân qua Personal Access Token.

---

## ⌨️ Bảng lệnh `sav`

```bash
sav [command] [arguments]
```

| Lệnh | Ý nghĩa | Ví dụ sử dụng |
| :--- | :--- | :--- |
| **`sav ls`** | Liệt kê các game có save trên máy | `sav ls` |
| **`sav lsc`** | Liệt kê các bản save hiện có trên GitHub | `sav lsc` |
| **`sav scan`**| Quét lại (rescan) toàn bộ database và save game | `sav scan` |
| **`sav push`** | Nén và đẩy game lên GitHub (chọn theo số hoặc tất cả) | `sav push`<br>`sav push 1`<br>`sav push all` |
| **`sav pull`** | Tải bản save từ GitHub và khôi phục vào máy theo số | `sav pull`<br>`sav pull 1`<br>`sav pull all` |
| **`sav status`**| Kiểm tra cấu hình và kết nối tới GitHub | `sav status` |
| **`sav config`**| Cài đặt / sửa cấu hình GitHub (Token, Owner, Repo) | `sav config`<br>`sav config token ghp_xxx` |
| **`sav add`** | Thêm game tùy chỉnh (nhập tên, đường dẫn thư mục save) | `sav add` |
| **`sav remove`**| Xóa game tùy chỉnh theo số chọn | `sav remove`<br>`sav remove 1` |
| **`sav help`** | Hiển thị bảng trợ giúp | `sav help` |

> 💡 **Mẹo**: Nếu bạn chỉ gõ `sav` (không kèm tham số), chương trình sẽ tự động mở **Interactive Menu** với các phím mũi tên điều hướng.

---

## 🚀 Cài đặt & Sử dụng

### Cách 1: Tải file Setup (Khuyên dùng - Tự động thêm vào PATH)
1. Vào mục [**Releases**](https://github.com/3ky4r0/ludusavo/releases) của repository.
2. Tải file `sav-vX.Y.Z-setup.exe` về máy.
3. Chạy file cài đặt (không cần quyền Admin). Trình cài đặt sẽ tự động:
   - Cài đặt `sav` vào máy.
   - **Tự động cấu hình biến môi trường `PATH`**.
4. Mở bất kỳ PowerShell/CMD nào và gõ ngay:
   ```bash
   sav ls
   sav push
   sav pull
   ```

### Cách 2: Chạy trực tiếp từ mã nguồn qua script
```powershell
.\sav.bat status
.\sav.bat ls
.\sav.bat push
```
