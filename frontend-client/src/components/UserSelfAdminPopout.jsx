import { useState, useRef, useEffect } from 'react';
import { createPortal } from 'react-dom';
import { User, LogOut, Key, Upload, Camera, CheckCircle, X, Shield, Zap } from 'lucide-react';
import { authService } from '../services/authService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useEpilepsySafeMode } from './EpilepsySafeMode.jsx';

const DEFAULT_AVATAR = 'https://api.dicebear.com/7.x/initials/svg?background=random';

export default function UserSelfAdminPopout({ isOpen, onClose, anchorRect }) {
  const { activeProfile, logout, updateLocalProfile } = useAuth();
  const { epilepsySafeMode, toggleEpilepsySafeMode } = useEpilepsySafeMode();
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [outOfOfficeSaving, setOutOfOfficeSaving] = useState(false);
  const [showPasswordForm, setShowPasswordForm] = useState(false);
  const [passwordError, setPasswordError] = useState('');
  const [passwordSuccess, setPasswordSuccess] = useState('');

  const fileInputRef = useRef(null);
  const dialogRef = useRef(null);
  const [profileSaving, setProfileSaving] = useState(false);
  const [profileMessage, setProfileMessage] = useState('');
  useEffect(() => {
    if (isOpen && dialogRef.current && !dialogRef.current.open) dialogRef.current.showModal();
  }, [isOpen]);
  const saveDetails = async (event) => {
    event.preventDefault();
    setProfileSaving(true);
    setProfileMessage('');
    try {
      await authService.updateProfile({ fullName: profile.fullName.trim(), licenseNumber: profile.licenseNumber || '' });
      setProfileMessage('Your details have been saved.');
      updateLocalProfile({ name: profile.fullName.trim() });
      const saved = JSON.parse(localStorage.getItem('user_profile_data') || '{}');
      localStorage.setItem('user_profile_data', JSON.stringify({ ...saved, name: profile.fullName.trim() }));
    } catch (error) { setProfileMessage(error.message || 'Could not save your details.'); }
    finally { setProfileSaving(false); }
  };

  useEffect(() => {
    if (isOpen) {
      loadProfile();
    }
  }, [isOpen]);

  const loadProfile = async () => {
    setLoading(true);
    setProfileMessage('');
    try {
      const data = await authService.getProfile();
      setProfile(data);
    } catch (err) {
      setProfileMessage(err.message || 'Could not load your profile. Please retry.');
    } finally {
      setLoading(false);
    }
  };

  const displayName = profile?.fullName || activeProfile?.name || 'User';
  const displayEmail = profile?.email || activeProfile?.email || '';
  const avatarUrl = authService.profilePictureUrl(profile?.avatarUrl || activeProfile?.avatar) || DEFAULT_AVATAR;

  const handlePickFile = () => {
    if (fileInputRef.current) fileInputRef.current.click();
  };

  const handleFileChange = async (e) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith('image/')) {
      alert('Please select an image file (png, jpg, webp).');
      return;
    }

    setUploading(true);
    try {
      const result = await authService.uploadProfilePicture(file);
      setProfile((prev) => ({ ...prev, avatarUrl: result.avatarUrl }));
      updateLocalProfile({ avatar: result.avatarUrl });
    } catch (err) {
      alert(err.message || 'Upload failed.');
    } finally {
      setUploading(false);
    }
  };

  const toggleOutOfOffice = async () => {
    if (!profile) return;
    setOutOfOfficeSaving(true);
    try {
      await authService.updateProfile({ outOfOffice: !profile.outOfOffice });
      setProfile((prev) => ({ ...prev, outOfOffice: !prev.outOfOffice }));
    } catch (err) {
      alert(err.message || 'Failed to update out-of-office status.');
    } finally {
      setOutOfOfficeSaving(false);
    }
  };

  const handlePasswordChange = async (e) => {
    e.preventDefault();
    const current = e.target.currentPassword.value;
    const newPass = e.target.newPassword.value;
    const confirm = e.target.confirmPassword.value;

    if (newPass !== confirm) {
      setPasswordError('New passwords do not match.');
      return;
    }

    if (newPass.length < 6) {
      setPasswordError('Password must be at least 6 characters.');
      return;
    }

    setPasswordError('');
    setPasswordSuccess('');
    try {
      await authService.changePassword(current, newPass);
      setPasswordSuccess('Password changed successfully.');
      e.target.reset();
    } catch (err) {
      setPasswordError(err.message || 'Failed to change password.');
    }
  };

  if (!isOpen) return null;

  return createPortal(
    <dialog ref={dialogRef} onCancel={onClose} aria-label="My profile" className="m-auto w-[calc(100%-2rem)] max-w-md max-h-[85dvh] overflow-y-auto rounded-2xl border border-slate-700 bg-slate-900 p-0 text-slate-100 shadow-2xl backdrop:bg-black/60">
      <button type="button" onClick={onClose} aria-label="Close profile" className="absolute right-3 top-3 p-2 text-slate-300"><X size={18} /></button>
        <div className="p-4 border-b border-slate-800 flex items-center gap-3">
          <div className="relative w-12 h-12 rounded-full overflow-hidden border-2 border-slate-700">
            <img
              src={avatarUrl}
              alt={displayName}
              className="w-full h-full object-cover"
              onError={(e) => {
                e.target.src = DEFAULT_AVATAR;
              }}
            />
            {uploading && (
              <div className="absolute inset-0 bg-black/50 flex items-center justify-center">
                <div className="w-4 h-4 border-2 border-white border-t-transparent rounded-full animate-spin" />
              </div>
            )}
          </div>
          <div>
            <p className="text-sm font-semibold text-white">{displayName}</p>
            <p className="text-xs text-slate-400 truncate max-w-[180px]">{displayEmail}</p>
          </div>
        </div>

        <div className="p-2">
          {!loading && !profile && profileMessage && <div role="alert" className="p-3 text-sm text-red-300">{profileMessage}<button type="button" onClick={loadProfile} className="ml-2 underline">Retry</button></div>}
          {loading ? <p role="status" className="p-3 text-sm">Loading profile…</p> : profile && <form onSubmit={saveDetails} className="space-y-3 p-3">
            <label className="block text-xs">Full name<input required maxLength={200} value={profile.fullName || ''} onChange={event => setProfile({ ...profile, fullName: event.target.value })} className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 p-2" /></label>
            <label className="block text-xs">License number<input maxLength={100} value={profile.licenseNumber || ''} onChange={event => setProfile({ ...profile, licenseNumber: event.target.value })} className="mt-1 w-full rounded-lg border border-slate-700 bg-slate-950 p-2" /></label>
            <p className="text-xs text-slate-400">Role: {profile.systemRole}. Email and role are managed by your administrator.</p>
            <button disabled={profileSaving} className="rounded-lg bg-blue-600 px-4 py-2 text-xs disabled:opacity-50">{profileSaving ? 'Saving…' : 'Save details'}</button>
            {profileMessage && <p role="status" className="text-xs">{profileMessage}</p>}
          </form>}
          <button
            type="button"
            onClick={handlePickFile}
            disabled={uploading}
            className="w-full flex items-center gap-2 px-3 py-2 text-xs text-slate-300 hover:bg-slate-800 hover:text-white rounded-lg transition-colors disabled:opacity-50 cursor-pointer"
          >
            <Camera size={14} />
            <span>Upload profile picture</span>
            <input
              type="file"
              ref={fileInputRef}
              accept="image/*"
              onChange={handleFileChange}
              className="hidden"
            />
          </button>

          <button
            type="button"
            onClick={toggleOutOfOffice}
            disabled={outOfOfficeSaving || !profile}
            className={`w-full flex items-center gap-2 px-3 py-2 text-xs rounded-lg transition-colors ${
              profile?.outOfOffice
                ? 'bg-amber-950/30 text-amber-300 hover:bg-amber-950/50'
                : 'text-slate-300 hover:bg-slate-800 hover:text-white'
            } disabled:opacity-50 cursor-pointer`}
          >
            <CheckCircle size={14} className={profile?.outOfOffice ? 'text-amber-400' : ''} />
            <span>Out of office {profile?.outOfOffice && '(on)'}</span>
          </button>

          <button
            type="button"
            onClick={() => toggleEpilepsySafeMode()}
            className={`w-full flex items-center gap-2 px-3 py-2 text-xs rounded-lg transition-colors ${
              epilepsySafeMode
                ? 'bg-indigo-950/30 text-indigo-300 hover:bg-indigo-950/50'
                : 'text-slate-300 hover:bg-slate-800 hover:text-white'
            } cursor-pointer`}
          >
            <Shield size={14} className={epilepsySafeMode ? 'text-indigo-400' : ''} />
            <span>Epilepsy-safe mode {epilepsySafeMode && '(on)'}</span>
          </button>

          <button
            type="button"
            onClick={() => setShowPasswordForm(!showPasswordForm)}
            className="w-full flex items-center gap-2 px-3 py-2 text-xs text-slate-300 hover:bg-slate-800 hover:text-white rounded-lg transition-colors cursor-pointer"
          >
            <Key size={14} />
            <span>Change password</span>
          </button>

          {showPasswordForm && (
            <form onSubmit={handlePasswordChange} className="mt-2 space-y-2 px-1">
              <input
                type="password"
                name="currentPassword"
                placeholder="Current password"
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                required
              />
              <input
                type="password"
                name="newPassword"
                placeholder="New password"
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                required
                minLength={6}
              />
              <input
                type="password"
                name="confirmPassword"
                placeholder="Confirm new password"
                className="w-full px-3 py-2 bg-slate-950 border border-slate-700 rounded-xl text-xs text-white focus:outline-none focus:border-blue-500"
                required
                minLength={6}
              />
              {passwordError && <p className="text-xs text-red-400">{passwordError}</p>}
              {passwordSuccess && <p className="text-xs text-emerald-400">{passwordSuccess}</p>}
              <button
                type="submit"
                className="w-full px-3 py-1.5 bg-blue-600 text-white text-xs font-semibold rounded-lg hover:bg-blue-500 transition-colors cursor-pointer"
              >
                Save password
              </button>
            </form>
          )}

          <div className="border-t border-slate-800 my-1" />

          <button
            type="button"
            onClick={logout}
            className="w-full flex items-center gap-2 px-3 py-2 text-xs text-slate-300 hover:bg-red-950/20 hover:text-red-400 rounded-lg transition-colors cursor-pointer"
          >
            <LogOut size={14} />
            <span>Sign Out</span>
          </button>
        </div>
    </dialog>, document.body
  );
}
