import { useState, useRef, useEffect } from 'react';
import { User, LogOut, Key, Upload, Camera, CheckCircle, X, Shield, Zap } from 'lucide-react';
import { authService } from '../services/authService.js';
import { useAuth } from '../contexts/AuthContext.jsx';
import { useEpilepsySafeMode } from './EpilepsySafeMode.jsx';

const DEFAULT_AVATAR = 'https://api.dicebear.com/7.x/initials/svg?background=random';

export default function UserSelfAdminPopout({ isOpen, onClose, anchorRect }) {
  const { activeProfile, logout } = useAuth();
  const { epilepsySafeMode, toggleEpilepsySafeMode } = useEpilepsySafeMode();
  const [profile, setProfile] = useState(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [outOfOfficeSaving, setOutOfOfficeSaving] = useState(false);
  const [showPasswordForm, setShowPasswordForm] = useState(false);
  const [passwordError, setPasswordError] = useState('');
  const [passwordSuccess, setPasswordSuccess] = useState('');

  const fileInputRef = useRef(null);

  useEffect(() => {
    if (isOpen) {
      loadProfile();
    }
  }, [isOpen]);

  const loadProfile = async () => {
    setLoading(true);
    try {
      const data = await authService.getProfile();
      setProfile(data);
    } catch (err) {
      console.warn('Failed to load profile for pop-out', err);
    } finally {
      setLoading(false);
    }
  };

  const displayName = profile?.fullName || activeProfile?.name || 'User';
  const displayEmail = profile?.email || activeProfile?.email || '';
  const avatarUrl = profile?.avatarUrl || activeProfile?.avatar || DEFAULT_AVATAR;

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

  const popoutWidth = 320;
  const popoutMaxHeight = window.innerHeight - 80;

  const position = anchorRect
    ? {
        position: 'fixed',
        top: Math.min(anchorRect.bottom + 8, window.innerHeight - 200),
        right: Math.max(8, window.innerWidth - anchorRect.right - popoutWidth),
        maxHeight: popoutMaxHeight,
        overflow: 'auto',
      }
    : {};

  return (
    <>
      <div
        className="fixed inset-0 z-40"
        onClick={onClose}
      />
      <div
        className="fixed z-50 bg-transparent"
        style={{
          top: anchorRect ? Math.min(anchorRect.bottom + 8, window.innerHeight - 200) : 8,
          right: anchorRect ? Math.max(8, window.innerWidth - anchorRect.right - popoutWidth) : 8,
        }}
      >
        <div
          className="bg-slate-900 border border-slate-700 rounded-2xl shadow-2xl w-80 max-w-sm"
          onClick={(e) => e.stopPropagation()}
          style={{ maxHeight: popoutMaxHeight, overflow: 'auto' }}
        >
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
      </div>
    </div>
    </>
  );
}
