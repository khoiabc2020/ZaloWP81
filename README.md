# ZaloWP81

Ứng dụng **Zalo Native Client** dành riêng cho các thiết bị chạy **Windows Phone 8.1** (Lumia 520, 525, 620, 625, 630, 720, 730, 820, 830, 920, 925, 928, 930, 1020, 1320, 1520,...).

---

## 📱 Tính năng chính

- **Giao diện Modern UI / Metro Pivot**: Thiết kế 3 tab mượt mà theo đúng ngôn ngữ thiết kế của Windows Phone 8.1 (*Tin nhắn*, *Danh bạ*, *Cá nhân*).
- **Đăng nhập quét mã QR**: Hiển thị mã QR trực tiếp trên màn hình, quét bằng app Zalo trên điện thoại chính để đăng nhập an toàn không cần mật khẩu.
- **Tự động lưu phiên**: Phiên đăng nhập được lưu trữ an toàn trong bộ nhớ máy, mở lại ứng dụng không cần quét QR lần 2.
- **Nhắn tin trực tiếp & Nhóm**:
  - Xem danh sách cuộc trò chuyện với thời gian thực và đếm số tin nhắn chưa đọc.
  - Bong bóng tin nhắn xanh/xám hiển thị rõ ràng người gửi và thời gian gửi.
  - Gửi tin nhắn văn bản nhanh chóng.
- **Tối ưu hóa hiệu năng**: Viết bằng C# WinRT thuần cho Windows Phone 8.1 ARM, dung lượng file cài đặt siêu nhẹ (~3.2 MB), khởi động tức thì và tiết kiệm RAM.

---

## 📥 Hướng dẫn cài đặt lên điện thoại

### Yêu cầu
- Điện thoại chạy **Windows Phone 8.1** (đã Developer Unlock / Interop Unlock).
- Công cụ **Windows Phone Application Deployment 8.1** trên PC (hoặc Windows Phone Power Tools).

### Các bước cài đặt
1. Tải file **`ZaloWP81_1.0.0.0_ARM.appx`** từ mục [Releases](https://github.com/khoiabc2020/ZaloWP81/releases).
2. Tải file chứng chỉ số `ZaloDev.cer` (hoặc cài đặt cert trước trên điện thoại).
3. Kết nối Lumia với máy tính qua cáp USB.
4. Mở **Windows Phone Developer Registration 8.1** để unlock điện thoại (nếu chưa unlock).
5. Mở **Windows Phone Application Deployment (WPAppDeploy)**:
   - Target: `Device`
   - App: Chọn file `ZaloWP81_1.0.0.0_ARM.appx`
   - Nhấn **Deploy**.
6. Ứng dụng Zalo sẽ xuất hiện trong danh sách App của điện thoại.

---

## 🛠 Hướng dẫn Build từ mã nguồn

### Yêu cầu hệ thống PC
- Windows 10 / Windows 11
- Visual Studio (với MSBuild)
- Windows Phone 8.1 SDK (`Windows Phone Kits\8.1`)
- Windows 10 SDK (cho `makeappx.exe` và `signtool.exe`)
- Python 3.x

### Lệnh biên dịch & đóng gói
Chạy script tự động:
```powershell
python build.py
```
Script sẽ tự động:
1. Biên dịch mã nguồn C# sang binary ARM Release (`ZaloWP81.exe`).
2. Chuẩn bị Asset và Manifest.
3. Đóng gói thành file `ZaloWP81_1.0.0.0_ARM.appx`.
4. Ký số bằng chứng chỉ số.

---

## 📜 Bản quyền & Giấy phép
Dự án được phát triển phi lợi nhuận nhằm mục đích bảo tồn và đem lại trải nghiệm ứng dụng Zalo cho cộng đồng đam mê Windows Phone.

