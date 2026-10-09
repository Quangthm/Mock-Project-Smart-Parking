import { useState, useRef } from 'react';
import { useApp } from '../../../context/AppContext';
import { ownerApi, type OwnerApplicationRecord } from '../../../lib/ownerApi';
import type { LotType } from '../../../lib/types';
import { BrandLogo } from '../../../components/brand/BrandLogo';
import { PasswordVisibilityIcon } from '../../../components/forms/PasswordVisibilityIcon';
import { UntitledIcon } from '../../../components/icon/UntitledIcon';
import { authApi, request, type DriverRegistration } from '../../../lib/authApi';
import { DriverOtpForm, pendingDriverKey, restoreDriverRegistration } from './DriverOtpForm';

import { OwnerVerification } from './OwnerVerification';

type Tab = 'driver' | 'business';

function validatePassword(pw: string) {
  const errors: string[] = [];
  if (pw.length < 8) errors.push('At least 8 characters');
  if (pw.length > 15) errors.push('Maximum 15 characters');
  if (!/[A-Z]/.test(pw)) errors.push('One uppercase letter');
  if (!/[a-z]/.test(pw)) errors.push('One lowercase letter');
  if (!/[0-9]/.test(pw)) errors.push('One number');
  if (!/[^A-Za-z0-9]/.test(pw)) errors.push('One special character');
  return errors;
}

function PasswordStrength({ pw }: { pw: string }) {
  const errs = validatePassword(pw);
  if (!pw) return null;
  const ok = 6 - errs.length;
  const color = ok <= 2 ? '#ef4444' : ok <= 4 ? '#f59e0b' : '#22c55e';
  const RULES = ['At least 8 characters', 'Maximum 15 characters', 'One uppercase letter', 'One lowercase letter', 'One number', 'One special character'];
  return (
    <div style={{ marginTop: '0.5rem' }}>
      <div style={{ height: '4px', background: 'var(--border)', borderRadius: '2px', overflow: 'hidden' }}>
        <div style={{ height: '100%', width: `${(ok / 6) * 100}%`, background: color, transition: 'width 0.3s' }} />
      </div>
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.3rem', marginTop: '0.375rem' }}>
        {RULES.map(r => {
          const passed = !errs.includes(r);
          return (
            <span key={r} style={{ fontSize: '0.72rem', color: passed ? '#22c55e' : 'var(--muted)', display: 'flex', alignItems: 'center', gap: '0.2rem' }}>
              {passed ? <UntitledIcon name="check" size={13} /> : <span aria-hidden="true">○</span>} {r}
            </span>
          );
        })}
      </div>
    </div>
  );
}

// T&C scroll-to-read modal
const PARTNER_TNC = `THỎA THUẬN ĐỐI TÁC SMARTPARKING

Phiên bản: 1.0 | Ngày hiệu lực: 01/01/2026

1. TỔNG QUAN
Thỏa thuận này được ký kết giữa SmartParking Technology Co., Ltd. ("SmartParking") và tổ chức/cá nhân đăng ký làm đối tác quản lý bãi đỗ xe ("Đối tác"). Bằng việc gửi đơn đăng ký, Đối tác xác nhận đã đọc, hiểu và đồng ý với toàn bộ các điều khoản dưới đây.

2. PHẠM VI HỢP TÁC
• Đối tác đồng ý cung cấp dịch vụ đỗ xe thông qua nền tảng SmartParking.
• SmartParking có quyền hiển thị thông tin bãi đỗ của Đối tác trên ứng dụng và website.
• Đối tác cam kết cung cấp thông tin chính xác về số lượng slot, giá cả, giờ hoạt động và các tiện ích.

3. PHÍ NỀN TẢNG VÀ CHIA SẺ DOANH THU
• SmartParking thu phí nền tảng là 15% trên mỗi giao dịch booking hoàn thành.
• Đối tác nhận 85% doanh thu từ mỗi booking.
• Thanh toán được thực hiện vào ngày 5 hàng tháng qua chuyển khoản ngân hàng.
• SmartParking cung cấp báo cáo chi tiết về doanh thu hàng tháng.

4. NGHĨA VỤ CỦA ĐỐI TÁC
• Duy trì hoạt động bãi đỗ xe theo tiêu chuẩn đã đăng ký.
• Đảm bảo an toàn tài sản cho khách hàng sử dụng dịch vụ.
• Tuân thủ chính sách hoàn tiền của SmartParking.
• Đối tác phải có giấy phép kinh doanh hợp lệ và đáp ứng các quy định pháp luật về hoạt động bãi đỗ xe.
• Không từ chối khách hàng có booking hợp lệ từ nền tảng SmartParking.
• Thông báo trước ít nhất 7 ngày nếu tạm ngừng dịch vụ.

5. NGHĨA VỤ CỦA SMARTPARKING
• Cung cấp hệ thống đặt chỗ và thanh toán hoạt động ổn định (cam kết uptime 98%).
• Hỗ trợ kỹ thuật trong giờ hành chính và khẩn cấp 24/7.
• Thanh toán đúng hạn theo thỏa thuận.
• Cung cấp dashboard quản lý cho Đối tác.

6. CHẤM DỨT HỢP TÁC
• Mỗi bên có thể chấm dứt hợp tác bằng văn bản thông báo trước 30 ngày.
• SmartParking có quyền chấm dứt ngay lập tức nếu Đối tác vi phạm nghiêm trọng điều khoản này.
• Khi chấm dứt, tất cả booking đang hoạt động sẽ được xử lý hoàn tất trước khi đóng tài khoản.

7. GIỚI HẠN TRÁCH NHIỆM
SmartParking không chịu trách nhiệm về mất mát, hư hỏng tài sản xảy ra trong bãi đỗ xe ngoài phạm vi thỏa thuận bảo hiểm riêng của Đối tác. Đối tác chịu trách nhiệm tuân thủ tất cả quy định pháp luật địa phương liên quan đến hoạt động bãi đỗ xe.

8. LUẬT ÁP DỤNG
Thỏa thuận này được điều chỉnh bởi pháp luật Việt Nam. Mọi tranh chấp được giải quyết tại Tòa án nhân dân có thẩm quyền tại Thành phố Hồ Chí Minh.

--- Vui lòng cuộn xuống đến cuối để đọc toàn bộ thỏa thuận ---`;

function TermsModal({ onAccept, onClose }: { onAccept: () => void; onClose: () => void }) {
  const scrollRef = useRef<HTMLDivElement>(null);
  const [readAll, setReadAll] = useState(false);

  function handleScroll() {
    const el = scrollRef.current;
    if (!el) return;
    if (el.scrollTop + el.clientHeight >= el.scrollHeight - 10) {
      setReadAll(true);
    }
  }

  return (
    <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.65)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 9999, padding: '1rem' }}>
      <div className="card" style={{ width: '100%', maxWidth: '560px', display: 'flex', flexDirection: 'column', maxHeight: '85vh' }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
          <h3 style={{ fontFamily: 'Outfit', fontWeight: 700, fontSize: '1.1rem', margin: 0, color: 'var(--fg)' }}>
            SmartParking Partner Agreement
          </h3>
          <button onClick={onClose} aria-label="Close agreement" style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--muted)', display: 'flex', padding: 4 }}><UntitledIcon name="x" size={18} /></button>
        </div>
        {!readAll && (
          <div style={{ background: '#fef9c3', border: '1px solid #fde68a', borderRadius: 'var(--radius)', padding: '0.5rem 0.75rem', marginBottom: '0.75rem', fontSize: '0.8rem', color: '#92400e' }}>
            <UntitledIcon name="file" size={16} /> Please scroll to the bottom to read the full agreement before accepting.
          </div>
        )}
        <div
          ref={scrollRef}
          onScroll={handleScroll}
          style={{ flex: 1, overflowY: 'auto', background: 'var(--bg)', borderRadius: 'var(--radius)', border: '1px solid var(--border)', padding: '1rem', fontSize: '0.82rem', lineHeight: 1.8, color: 'var(--muted)', whiteSpace: 'pre-wrap', fontFamily: 'Inter, sans-serif' }}
        >
          {PARTNER_TNC}
        </div>
        <div style={{ marginTop: '1rem', display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
          <button className="btn-outline" onClick={onClose} style={{ fontSize: '0.875rem' }}>Cancel</button>
          <button
            className="btn-primary"
            onClick={onAccept}
            disabled={!readAll}
            style={{ fontSize: '0.875rem', opacity: readAll ? 1 : 0.45, cursor: readAll ? 'pointer' : 'not-allowed' }}
          >
            {readAll ? 'I Accept' : 'Scroll to read all ↓'}
          </button>
        </div>
      </div>
    </div>
  );
}

export function SignUp() {
  const { setView } = useApp();
  const [tab, setTab] = useState<Tab>('driver');
  const [error, setError] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [showTnC, setShowTnC] = useState(false);
  const [registering, setRegistering] = useState(false);
  const [registration, setRegistration] = useState<DriverRegistration | null>(restoreDriverRegistration);
  const [ownerRegistration,setOwnerRegistration]=useState<Pick<OwnerApplicationRecord,'email'|'verification'>|null>(()=>{try{const value=JSON.parse(localStorage.getItem('sp_pending_owner_registration')??'null');return typeof value?.email==='string' && value?.verification?value:null;}catch{return null;}});
  function saveRegistration(value: DriverRegistration) {
    setRegistration(value);
    // Store only the opaque challenge and timing; never store the password or OTP.
    try { localStorage.setItem(pendingDriverKey, JSON.stringify(value)); } catch { /* Current tab can still verify. */ }
  }

  const [driverForm, setDriverForm] = useState({ name: '', email: '', phone: '', password: '', confirmPassword: '' });
  const [ownerForm, setOwnerForm] = useState({
    name: '', businessName: '', email: '', phone: '', password: '', confirmPassword: '',
    lotType: 'outdoor' as LotType, agreedToPolicy: false,
  });

  async function submitDriver(e: React.FormEvent) {
    e.preventDefault();
    if (registering) return;
    setError('');
    const pwErrors = driverForm.password?validatePassword(driverForm.password):[];
    if (pwErrors.length) { setError(`Password issue: ${pwErrors[0]}`); return; }
    if (driverForm.password !== driverForm.confirmPassword) { setError('Passwords do not match.'); return; }
    if (!driverForm.email.trim() && !driverForm.phone.trim()) { setError('Enter an email or phone number.'); return; }
    setRegistering(true);
    try {
      saveRegistration(await authApi.registerDriver({ fullName: driverForm.name.trim(),
        email: driverForm.email.trim() || undefined, phone: driverForm.phone.trim() || undefined, password: driverForm.password || undefined }));
      setDriverForm(f => ({ ...f, password: '', confirmPassword: '' }));
    } catch (e) {
      try{saveRegistration(await authApi.recoverDriver(driverForm.email.trim()||driverForm.phone.trim()));setDriverForm(f=>({...f,password:'',confirmPassword:''}));}
      catch{setError(e instanceof Error ? e.message : 'Registration failed.');}
    }
    finally { setRegistering(false); }
  }

  async function submitOwner(e: React.FormEvent) {
    e.preventDefault();
    if (registering) return;
    setError('');
    if (!ownerForm.agreedToPolicy) { setError('You must read and agree to the Partner Agreement.'); return; }
    const pwErrors = ownerForm.password?validatePassword(ownerForm.password):[];
    if (pwErrors.length) { setError(`Password issue: ${pwErrors[0]}`); return; }
    if (ownerForm.password !== ownerForm.confirmPassword) { setError('Passwords do not match.'); return; }
    setRegistering(true);
    try {
      const application=await ownerApi.register({ fullName: ownerForm.name.trim(), businessName: ownerForm.businessName.trim(),
        email: ownerForm.email.trim(), phone: ownerForm.phone.trim(), password: ownerForm.password || undefined,
        lotType: ownerForm.lotType, agreedToPolicy: ownerForm.agreedToPolicy });
      setOwnerForm(f => ({ ...f, password: '', confirmPassword: '' }));
      setOwnerRegistration(application);try{localStorage.setItem('sp_pending_owner_registration',JSON.stringify(application));}catch{ /* This tab retains its challenges. */ }
    } catch (e) {
      try{
        const recovered=await request<{data:Pick<OwnerApplicationRecord,'verification'>}>('/register/owner/recover','POST',{contact:ownerForm.email.trim()});
        const pending={email:ownerForm.email.trim(),...recovered.data};setOwnerRegistration(pending);setOwnerForm(f=>({...f,password:'',confirmPassword:''}));
        try{localStorage.setItem('sp_pending_owner_registration',JSON.stringify(pending));}catch{/* Current tab retains its challenges. */}
      }catch{setError(e instanceof Error ? e.message : 'Registration failed.');}
    }
    finally { setRegistering(false); }
  }

  const tabStyle = (t: Tab) => ({
    display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: '0.45rem', flex: 1, padding: '0.625rem', fontWeight: 600, fontSize: '0.925rem', cursor: 'pointer', border: 'none',
    background: tab === t ? 'var(--primary)' : 'var(--card)',
    color: tab === t ? 'var(--primary-fg)' : 'var(--muted)',
    transition: 'all 0.2s',
  });

  return (
    <div className="auth-page-background" style={{ minHeight: '80vh', display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '2rem 1rem' }}>
      {showTnC && (
        <TermsModal
          onAccept={() => { setOwnerForm(f => ({ ...f, agreedToPolicy: true })); setShowTnC(false); }}
          onClose={() => setShowTnC(false)}
        />
      )}
      <div style={{ width: '100%', maxWidth: '480px' }}>
        <div style={{ textAlign: 'center', marginBottom: '1.75rem' }}>
          <BrandLogo height={100} style={{ margin: '0 auto 1rem' }} />
          <h1 style={{ fontFamily: 'Outfit', fontWeight: 800, fontSize: '1.75rem', color: 'var(--fg)', margin: '0 0 0.375rem' }}>Create Account</h1>
          <p style={{ color: 'var(--muted)', fontSize: '0.925rem' }}>Join SmartParking today</p>
        </div>

        {!registration && !ownerRegistration && <div style={{ display: 'flex', border: '1px solid var(--border)', borderRadius: 'var(--radius)', overflow: 'hidden', marginBottom: '1.5rem' }}>
          <button style={tabStyle('driver')} onClick={() => setTab('driver')}><UntitledIcon name="car" size={16} /> Driver</button>
          <button style={tabStyle('business')} onClick={() => setTab('business')}><UntitledIcon name="building" size={16} /> Business</button>
        </div>}

        {ownerRegistration ? <OwnerVerification application={ownerRegistration} onVerified={()=>{try{localStorage.removeItem('sp_pending_owner_registration');localStorage.removeItem('sp_owner_verification_step');}catch{/* This tab can continue. */}setOwnerRegistration(null);setView('pending-approval');}}/> : registration ? <DriverOtpForm registration={registration} onUpdate={saveRegistration} onBack={() => setView('sign-in')}
          onDifferentContact={() => { try { localStorage.removeItem(pendingDriverKey); } catch { /* No persistent storage. */ } setRegistration(null); }}
          onVerified={() => { try { localStorage.removeItem(pendingDriverKey); } catch { /* No persistent storage. */ } setRegistration(null); setView('sign-in'); }} /> : tab === 'driver' ? (
          <form onSubmit={submitDriver} className="card auth-form-background" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <Field label="Full Name"><input className="input" value={driverForm.name} onChange={e => setDriverForm(f => ({ ...f, name: e.target.value }))} placeholder="Your full name" required style={{ fontSize: '1rem' }} /></Field>
            <Field label="Email"><input className="input" type="email" value={driverForm.email} onChange={e => setDriverForm(f => ({ ...f, email: e.target.value }))} placeholder="you@example.com" style={{ fontSize: '1rem' }} /></Field>
            <Field label="Phone Number"><input className="input" value={driverForm.phone} onChange={e => setDriverForm(f => ({ ...f, phone: e.target.value }))} placeholder="09xx-xxx-xxx" style={{ fontSize: '1rem' }} /></Field>
            <Field label="Password (optional; leave blank to use OTP sign-in)">
              <div style={{ position: 'relative' }}>
                <input className="input" type={showPassword ? 'text' : 'password'} value={driverForm.password} onChange={e => setDriverForm(f => ({ ...f, password: e.target.value }))} placeholder="Optional, 8–15 characters" style={{ paddingRight: '2.5rem', fontSize: '1rem' }} />
                <button type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} title={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)} style={{ position: 'absolute', right: '0.75rem', top: '50%', transform: 'translateY(-50%)', display: 'flex', width: 20, height: 20, padding: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--muted)' }}><PasswordVisibilityIcon visible={showPassword} /></button>
              </div>
              <PasswordStrength pw={driverForm.password} />
            </Field>
            <Field label="Confirm Password"><input className="input" type="password" value={driverForm.confirmPassword} onChange={e => setDriverForm(f => ({ ...f, confirmPassword: e.target.value }))} placeholder="Repeat your password" required={!!driverForm.password} style={{ fontSize: '1rem' }} /></Field>
            {error && <Err>{error}</Err>}
            <button type="submit" className="btn-primary" disabled={registering} style={{ width: '100%', justifyContent: 'center', fontSize: '1rem', padding: '0.75rem' }}>{registering ? 'Creating Account…' : 'Create Driver Account'}</button>
            <p style={{ textAlign: 'center', fontSize: '0.9rem', color: 'var(--muted)', margin: 0 }}>
              Already have an account?{' '}
              <button type="button" onClick={() => setView('sign-in')} style={{ background: 'none', border: 'none', color: 'var(--primary)', cursor: 'pointer', fontWeight: 600, padding: 0, fontSize: '0.9rem' }}>Sign In</button>
            </p>
          </form>
        ) : (
          <form onSubmit={submitOwner} className="card auth-form-background" style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ background: 'var(--primary)10', border: '1px solid var(--primary)30', borderRadius: 'var(--radius)', padding: '0.75rem', fontSize: '0.8rem', color: 'var(--muted)' }}>
              <strong style={{ color: 'var(--primary)' }}>Business Partnership Registration</strong><br />
              Submit your application. Our team will review and contact you within 2–3 business days.
            </div>
            <Field label="Contact Person Name"><input className="input" value={ownerForm.name} onChange={e => setOwnerForm(f => ({ ...f, name: e.target.value }))} placeholder="Your full name" required style={{ fontSize: '1rem' }} /></Field>
            <Field label="Business Name"><input className="input" value={ownerForm.businessName} onChange={e => setOwnerForm(f => ({ ...f, businessName: e.target.value }))} placeholder="Company or lot name" required style={{ fontSize: '1rem' }} /></Field>
            <Field label="Business Email"><input className="input" type="email" value={ownerForm.email} onChange={e => setOwnerForm(f => ({ ...f, email: e.target.value }))} placeholder="business@company.com" required style={{ fontSize: '1rem' }} /></Field>
            <Field label="Business Phone"><input className="input" value={ownerForm.phone} onChange={e => setOwnerForm(f => ({ ...f, phone: e.target.value }))} placeholder="0901-xxx-xxx" style={{ fontSize: '1rem' }} /></Field>
            <Field label="Parking Lot Type">
              <select className="input" value={ownerForm.lotType} onChange={e => setOwnerForm(f => ({ ...f, lotType: e.target.value as LotType }))} style={{ fontSize: '1rem' }}>
                <option value="outdoor">Outdoor Lot</option>
                <option value="basement">Basement Parking</option>
                <option value="multi-storey">Multi-Storey Parking</option>
              </select>
            </Field>
            <Field label="Password (optional; leave blank to use OTP sign-in)">
              <div style={{ position: 'relative' }}>
                <input className="input" type={showPassword ? 'text' : 'password'} value={ownerForm.password} onChange={e => setOwnerForm(f => ({ ...f, password: e.target.value }))} placeholder="Optional, 8–15 characters" style={{ paddingRight: '2.5rem', fontSize: '1rem' }} />
                <button type="button" aria-label={showPassword ? 'Hide password' : 'Show password'} title={showPassword ? 'Hide password' : 'Show password'} onClick={() => setShowPassword(!showPassword)} style={{ position: 'absolute', right: '0.75rem', top: '50%', transform: 'translateY(-50%)', display: 'flex', width: 20, height: 20, padding: 0, background: 'none', border: 'none', cursor: 'pointer', color: 'var(--muted)' }}><PasswordVisibilityIcon visible={showPassword} /></button>
              </div>
              <PasswordStrength pw={ownerForm.password} />
            </Field>
            <Field label="Confirm Password"><input className="input" type="password" value={ownerForm.confirmPassword} onChange={e => setOwnerForm(f => ({ ...f, confirmPassword: e.target.value }))} placeholder="Repeat password" required={!!ownerForm.password} style={{ fontSize: '1rem' }} /></Field>

            {/* T&C — must click to read, shows modal, can't check until read */}
            <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.625rem' }}>
              <input type="checkbox" id="tnc" checked={ownerForm.agreedToPolicy} onChange={() => !ownerForm.agreedToPolicy && setShowTnC(true)} style={{ marginTop: '3px', flexShrink: 0, width: 16, height: 16, cursor: 'pointer' }} />
              <label htmlFor="tnc" style={{ fontSize: '0.85rem', color: 'var(--muted)', lineHeight: 1.5, cursor: 'pointer' }}>
                I have read and agree to the SmartParking{' '}
                <button type="button" onClick={() => setShowTnC(true)} style={{ background: 'none', border: 'none', color: 'var(--primary)', cursor: 'pointer', padding: 0, fontSize: '0.85rem', textDecoration: 'underline', fontWeight: 600 }}>
                  Partner Terms & Conditions
                </button>
                {ownerForm.agreedToPolicy && <span style={{ color: '#22c55e', fontWeight: 600, marginLeft: '0.375rem', display: 'inline-flex', alignItems: 'center', gap: 3 }}><UntitledIcon name="check" size={14} /> Accepted</span>}
              </label>
            </div>

            {error && <Err>{error}</Err>}
            <button type="submit" disabled={registering} className="btn-accent" style={{ width: '100%', justifyContent: 'center', fontSize: '1rem', padding: '0.75rem' }}>
              {registering ? 'Submitting...' : 'Submit Partnership Application'}
            </button>
            <p style={{ textAlign: 'center', fontSize: '0.9rem', color: 'var(--muted)', margin: 0 }}>
              Already have an account?{' '}
              <button type="button" onClick={() => setView('sign-in')} style={{ background: 'none', border: 'none', color: 'var(--primary)', cursor: 'pointer', fontWeight: 600, padding: 0, fontSize: '0.9rem' }}>Sign In</button>
            </p>
            <div style={{ fontSize: '0.78rem', color: 'var(--muted)', textAlign: 'center', borderTop: '1px solid var(--border)', paddingTop: '0.75rem' }}>
              <strong>Operator accounts</strong> are created by lot Owners in their dashboard — not available for self-registration.
            </div>
          </form>
        )}
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <label className="label" style={{ fontSize: '0.925rem' }}>{label}</label>
      {children}
    </div>
  );
}

function Err({ children }: { children: React.ReactNode }) {
  return (
    <div style={{ background: '#fee2e2', border: '1px solid #fca5a5', borderRadius: 'var(--radius)', padding: '0.625rem 0.875rem', color: '#dc2626', fontSize: '0.875rem' }}>
      {children}
    </div>
  );
}
