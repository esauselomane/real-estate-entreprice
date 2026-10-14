/** @type {import('next').NextConfig} */
const nextConfig = {
  reactStrictMode: true,
  distDir: process.env.BISCOVE_PREVIEW_DIST_DIR || ".next",
  images: {
    unoptimized: false,
  },
  output: 'standalone',
};

export default nextConfig;
