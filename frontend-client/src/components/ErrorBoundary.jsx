import { Component } from 'react';
import { AlertTriangle, RefreshCw } from 'lucide-react';

/**
 * Class-based React Error Boundary. Catches render errors thrown by any
 * child component tree and renders a fallback UI instead of crashing the
 * whole application.
 *
 * Usage:
 *   <ErrorBoundary>
 *     <CRMWorkspace />
 *   </ErrorBoundary>
 */
export default class ErrorBoundary extends Component {
  constructor(props) {
    super(props);
    this.state = {
      hasError: false,
      error: null,
      errorInfo: null,
    };
  }

  static getDerivedStateFromError(error) {
    // Update state so the next render shows the fallback UI.
    return { hasError: true, error };
  }

  componentDidCatch(error, errorInfo) {
    // Log the error to the console (or send to an error-reporting service).
    console.error('ErrorBoundary caught an error:', error, errorInfo);
    this.setState({ errorInfo });
  }

  handleReset = () => {
    this.setState({ hasError: false, error: null, errorInfo: null });
  };

  render() {
    if (this.state.hasError) {
      // Allow the consumer to supply a custom fallback; otherwise use the
      // default recovery card.
      if (this.props.fallback) {
        return this.props.fallback;
      }
      return (
        <div className="p-6 bg-red-950/30 border border-red-900/60 rounded-2xl text-center space-y-4 max-w-lg mx-auto">
          <div className="w-14 h-14 rounded-2xl bg-red-950/60 border border-red-800 flex items-center justify-center mx-auto">
            <AlertTriangle size={28} className="text-red-400" />
          </div>
          <div>
            <h3 className="text-base font-bold text-red-300">Something went wrong</h3>
            <p className="text-xs text-red-400 mt-1">
              This section hit an unexpected error and couldn't render. The rest of the app is still running.
            </p>
            {this.state.error && (
              <pre className="mt-3 text-[10px] text-red-500 bg-red-950/40 rounded-lg p-2 overflow-auto text-left">
                {this.state.error.message || String(this.state.error)}
              </pre>
            )}
          </div>
          <button
            type="button"
            onClick={this.handleReset}
            className="inline-flex items-center gap-2 text-xs font-bold text-white bg-red-700 hover:bg-red-600 px-4 py-2 rounded-xl transition-colors cursor-pointer"
          >
            <RefreshCw size={13} /> Try again
          </button>
        </div>
      );
    }

    return this.props.children;
  }
}