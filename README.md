# sav (Game Save CLI)

[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?logo=windows)](https://www.microsoft.com/windows)
[![Storage](https://img.shields.io/badge/Storage-Google_Drive-4285F4?logo=googledrive)](https://drive.google.com/)
[![License](https://img.shields.io/badge/License-MIT-blue)](LICENSE)

Công cụ dòng lệnh quản lý, sao lưu và đồng bộ save game lên Google Drive cá nhân, sử dụng cơ sở dữ liệu từ Ludusavi.

---

## Tính năng

- Nhận diện vị trí file save tự động trên Windows (`%APPDATA%`, `%LOCALAPPDATA%`, `Saved Games`, `Documents`,...).
- Đồng bộ trực tiếp với Google Drive qua OAuth2.
- Nén ZIP kèm file ánh xạ đường dẫn (mapping) phục hồi.
- Hỗ trợ chọn một game, nhiều game cách nhau bởi khoảng trắng (`1 3 5`), hoặc toàn bộ (`all`).
- Hỗ trợ chế độ dòng lệnh trực tiếp và menu tương tác.

---

## Lệnh hỗ trợ

```bash
sav [command] [arguments]
```

| Lệnh | Mô tả | Ví dụ |
| :--- | :--- | :--- |
| `sav ls` | Liệt kê các game có save trên máy | `sav ls` |
| `sav lsc` | Liệt kê các bản save hiện có trên Google Drive | `sav lsc` |
| `sav scan` | Quét lại toàn bộ save game trên máy | `sav scan` |
| `sav push` | Nén và tải save lên Google Drive | `sav push`<br>`sav push 1`<br>`sav push 1 3 5`<br>`sav push all` |
| `sav pull` | Tải save từ Google Drive và khôi phục vào máy | `sav pull`<br>`sav pull 1`<br>`sav pull 1 3 5`<br>`sav pull all` |
| `sav status` | Kiểm tra kết nối và trạng thái tài khoản Google Drive | `sav status` |
| `sav login` | Đăng nhập tài khoản Google Drive qua trình duyệt | `sav login` |
| `sav logout` | Đăng xuất tài khoản Google Drive | `sav logout` |
| `sav config` | Xem hoặc sửa Google Client ID và Secret | `sav config` |
| `sav add` | Thêm game tùy chỉnh vào danh sách theo dõi | `sav add` |
| `sav remove` | Xóa game tùy chỉnh | `sav remove 1` |
| `sav help` | Hiển thị bảng trợ giúp | `sav help` |

> Chạy lệnh `sav` không kèm tham số sẽ hiển thị danh sách các lệnh trợ giúp.

---

## Cài đặt & Sử dụng

### Dùng bộ cài đặt (Khuyên dùng)
1. Tải file `sav-setup.exe` từ mục Releases.
2. Chạy file cài đặt để phần mềm tự động tích hợp vào biến môi trường `PATH`.
3. Mở Terminal / PowerShell và sử dụng lệnh `sav`.

### Chạy từ mã nguồn
```powershell
dotnet build -c Release
.\sav.bat status
.\sav.bat push 1 3 5
```

