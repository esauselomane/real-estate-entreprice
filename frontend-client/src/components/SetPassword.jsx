import { useState } from 'react';
import { Check, Loader2, Eye, EyeOff } from 'lucide-react';
import { authService } from '../services/authService.js';

export default function SetPassword({ activationToken, recoveryToken, onPasswordSet }) {
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [state, setState] = useState('idle'); // idle | submitting | success | error
  const [error, setError] = useState('');
  const [password, setPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');

  if (!activationToken && !recoveryToken) {
    return (
      <div className="min-h-screen w-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-zinc-950 p-6">
        <div className="w-full max-w-md bg-slate-900/60 backdrop-blur-xl border border-slate-700/50 p-8 rounded-2xl shadow-2xl shadow-black/50">
          <h2 className="text-2xl font-bold text-white mb-4">Invalid Link</h2>
          <p className="text-slate-400 text-sm">The activation link is missing or invalid.</p>
        </div>
      </div>
    );
  }
  

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');

    if (password.length < (recoveryToken ? 8 : 6)) {
      setError(`Password must be at least ${recoveryToken ? 8 : 6} characters.`);
      return;
    }
    if (password !== confirmPassword) {
      setError('Passwords do not match.');
      return;
    }

    setState('submitting');
    try {
      if (recoveryToken) await authService.resetPassword(recoveryToken, password, confirmPassword);
      else await authService.setTrialPassword(activationToken, password);
      setState('success');
      if (onPasswordSet) onPasswordSet();
    } catch (err) {
      setError(err.message || 'Failed to set password.');
      setState('error');
    }
  };

  return (
    <div className="min-h-screen w-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-zinc-950 p-6 overflow-hidden">

      <div className="absolute top-1/4 left-1/4 w-96 h-96 bg-blue-500/10 rounded-full blur-3xl pointer-events-none"></div>
      <div className="absolute bottom-1/4 right-1/4 w-96 h-96 bg-emerald-500/5 rounded-full blur-3xl pointer-events-none"></div>

      <div className="w-full max-w-md bg-slate-900/60 backdrop-blur-xl border border-slate-700/50 p-8 rounded-2xl shadow-2xl shadow-black/50">

        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-14 h-14 rounded-xl bg-blue-600 shadow-lg shadow-blue-500/30 mb-4">
            <svg className="w-7 h-7 text-white" fill="none" stroke="currentColor" strokeWidth="2" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 17h5m0 0l-1.69-1.69A.998.998 0 0118 13V5a2 2 0 10-4 0v8c0 .54.21 1.05.59 1.41L15 17z" />
            </svg>
          </div>
          <h2 className="text-2xl font-bold text-white">Set Your Password</h2>
          <p className="text-slate-400 text-sm mt-1">Create a secure password for your account.</p>
        </div>

        {state === 'success' && (
          <div className="flex items-center gap-2 rounded-lg bg-emerald-950/40 border border-emerald-900 p-3 text-sm text-emerald-300 mb-4">
            <Check size={16} />
            <span>Password set successfully. Your account is now active.</span>
          </div>
        )}
        {state === 'error' && (
          <div className="rounded-lg bg-red-950/40 border border-red-900 p-3 text-sm text-red-300 mb-4">
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div>
            <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">
              New Password
            </label>
            <div className="relative">
              <input
                type={showPassword ? "text" : "password"}
                required
                minLength={recoveryToken ? 8 : 6}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                disabled={state === 'submitting' || state === 'success'}
                placeholder="••••••••"
                className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200 disabled:opacity-50"
              />
              <button
                type="button"
                tabIndex={-1}
                onClick={() => setShowPassword(!showPassword)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-300"
              >
                {showPassword ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </div>

          <div>
            <label className="block text-slate-300 text-xs font-semibold uppercase tracking-wider mb-2">
              Confirm Password
            </label>
            <div className="relative">
              <input
                type={showConfirm ? "text" : "password"}
                required
                minLength={recoveryToken ? 8 : 6}
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
                disabled={state === 'submitting' || state === 'success'}
                placeholder="••••••••"
                className="w-full px-4 py-3 bg-slate-950/50 border border-slate-700/60 rounded-xl text-white placeholder-slate-500 focus:outline-none focus:border-blue-500 focus:ring-1 focus:ring-blue-500 transition-all duration-200 disabled:opacity-50"
              />
              <button
                type="button"
                tabIndex={-1}
                onClick={() => setShowConfirm(!showConfirm)}
                className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-300"
              >
                {showConfirm ? <EyeOff size={18} /> : <Eye size={18} />}
              </button>
            </div>
          </div>

          <button
            type="submit"
            disabled={state === 'submitting' || state === 'success'}
            className="w-full py-3 mt-2 bg-blue-600 hover:bg-blue-500 active:bg-blue-700 disabled:bg-blue-800 disabled:text-slate-400 text-white font-semibold rounded-xl shadow-lg shadow-blue-600/20 hover:shadow-blue-500/30 transition-all duration-200 flex items-center justify-center gap-2"
          >
            {state === 'submitting' && <Loader2 size={16} className="animate-spin" />}
            {state === 'submitting' ? 'Setting Password...' : 'Set Password'}
          </button>
        </form>

        {state === 'success' && (
          <div className="mt-6 text-center">
            <button
              onClick={() => window.location.reload()}
              className="text-blue-400 hover:text-blue-300 text-sm font-medium"
            >
              Back to Login
            </button>
          </div>
        )}
      </div>
    </div>
  );
}
